using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class AutoReviewEncryptedCccdTests
{
    private sealed class ReviewSaved : SaveChangesInterceptor
    {
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<SensitiveDataLog>().Any()) Completion.TrySetResult(true);
            return ValueTask.FromResult(result);
        }
    }
    [Test]
    public async Task Review_UsesHashForRandomCiphertexts_KeepsIpRule_AndLogsSystemActor()
    {
        var crypto = EncryptionServiceTests.Create();
        var name = Guid.NewGuid().ToString();
        var system = new Mock<IUser>();
        system.SetupGet(x => x.IsSystem).Returns(true);
        var services = new ServiceCollection();
        var saved = new ReviewSaved();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(name).AddInterceptors(saved));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IEncryptionService>(crypto);
        services.AddSingleton(system.Object);
        services.AddScoped<ISensitiveDataLogger, SensitiveDataLogger>();
        await using var provider = services.BuildServiceProvider();
        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var (id, cccd) in new[] { (1, "001234567890"), (2, "001234567890"), (3, "991234567890"), (4, (string?)null), (5, "551234567890"), (6, "661234567890") })
                db.Users.Add(new User { Id = id, Username = "fixture" + id, ApprovalStatus = "PendingApproval",
                    CCCD = cccd == null ? null : crypto.Encrypt(cccd), CccdLookupHash = cccd == null ? null : crypto.ComputeHmac("cccd:" + cccd),
                    LastLoginIp = id > 4 ? "192.0.2.10" : null, Latitude = id > 4 ? 21 : null, Longitude = id > 4 ? 105 : null });
            // 7 banned with a bank account; 8 comes back with the same account typed differently.
            db.Users.Add(new User { Id = 7, Username = "banned", Status = "Banned", BankAccountMock = crypto.Encrypt(@"{""accountNumber"":""0011223344""}") });
            db.Users.Add(new User { Id = 8, Username = "returning", ApprovalStatus = "PendingApproval", BankAccountMock = crypto.Encrypt(@"{""accountNumber"":""0011-2233-44""}") });
            // 9 is the same gmail mailbox as active user 10, written with dots and a +tag.
            db.Users.Add(new User { Id = 9, Username = "dots", Email = "N.guyen+shop2@gmail.com", ApprovalStatus = "PendingApproval" });
            db.Users.Add(new User { Id = 10, Username = "original", Email = "nguyen@gmail.com" });
            await db.SaveChangesAsync();
        }
        using var service = new AutoReviewUsersService(provider, NullLogger<AutoReviewUsersService>.Instance);
        await service.StartAsync(default);
        await saved.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(default);
        using var check = provider.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await context.Users.OrderBy(x => x.Id).ToListAsync();
        // 1,2 same CCCD: rejected. 5,6 only share IP + location (shared Wi-Fi?): left for an admin, not banned.
        // 8 shares a bank account with banned 7: rejected. 9 is user 10's mailbox: review.
        Assert.That(users.Select(x => x.ApprovalStatus), Is.EqualTo(new[]
            { "Rejected", "Rejected", "Approved", "Approved", "PendingApproval", "PendingApproval", "Approved", "Rejected", "PendingApproval", "Approved" }));
        Assert.That(users.Where(x => x.Id is 5 or 6 or 9).All(x => x.RiskCheckedAt != null && x.RiskScore >= 30), Is.True);
        Assert.That(users[7].BannedReason, Does.Not.Contain("Bank"), "the user is not told what linked them");
        Assert.That(await context.UserIdentifiers.CountAsync(i => i.Type == "Bank"), Is.EqualTo(2));
        var logs = await context.SensitiveDataLogs.ToListAsync();
        Assert.That(logs, Has.Count.EqualTo(8));
        Assert.That(logs.All(x => x.ActorId == 0 && x.ActorRole == "System"), Is.True);
    }
}
