using EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;
using EbayClone.Application.Admin.Disputes.Commands.UpdateDisputePriority;
using EbayClone.Application.AdminRoles.Commands.CreateAdminRole;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Products.Commands.ResolveProductViolation;
using EbayClone.Application.ReturRequests.Commands.ApproveReturnRequest;
using EbayClone.Application.ReturRequests.Commands.FreezeReturnRequest;
using EbayClone.Application.ReturRequests.Commands.ProcessReturnLabel;
using EbayClone.Application.ReturRequests.Commands.RejectReturnRequest;
using EbayClone.Application.Users.Commands.ApplySellerPenalty;
using EbayClone.Application.Withdrawals.Commands.ApproveWithdrawal;
using EbayClone.Application.Withdrawals.Commands.RejectWithdrawal;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class ActorIntegrityTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("not-an-id")]
    public void MissingOrInvalidAuthenticatedActor_IsRejected(string? id)
    {
        Assert.Throws<UnauthorizedAccessException>(() => SensitiveLogTests.Actor(id).GetRequiredActorId());
    }

    [Test]
    public void SystemIdentity_CannotActAsAuthenticatedAdmin()
    {
        var user = new Moq.Mock<IUser>();
        user.SetupGet(x => x.Id).Returns("42");
        user.SetupGet(x => x.IsSystem).Returns(true);
        Assert.Throws<UnauthorizedAccessException>(() => user.Object.GetRequiredActorId());
    }

    [Test]
    public async Task CreateRole_UsesAuthenticatedActorInsteadOfBody()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await new CreateAdminRoleCommandHandler(db, SensitiveLogTests.Actor("42", "SuperAdmin"))
            .Handle(new() { RoleName = "Fixture", CreatedBy = 999 }, default);
        Assert.That((await db.AdminActions.SingleAsync()).AdminId, Is.EqualTo(42));
    }

    [Test]
    public async Task SellerPenalty_RejectsMissingActorBeforeMutation_EvenWithBodyActor()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { Id = 1, PerformanceScore = 100 });
        await db.SaveChangesAsync();
        var handler = new ApplySellerPenaltyCommandHandler(db, SensitiveLogTests.Actor(null));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(1, 10, "Fixture", 999), default));
        Assert.That((await db.Users.SingleAsync()).PerformanceScore, Is.EqualTo(100));
        Assert.That(db.ChangeTracker.HasChanges(), Is.False);
        Assert.That(await db.AdminActions.CountAsync(), Is.Zero);
    }
    // Admin writes must resolve the actor before touching the DB: an unauthenticated call
    // gets 401 (not 404), never a request-supplied or "first admin in DB" fallback.
    private static IEnumerable<TestCaseData> AdminWritesWithoutActor()
    {
        IUser none = SensitiveLogTests.Actor(null);
        yield return Case("ProcessReturnLabel", db => new ProcessReturnLabelCommandHandler(db, Mock.Of<INotificationNotifier>(), none).Handle(new(1, "https://carrier.test/l.pdf"), default));
        yield return Case("ApproveReturnRequest", db => new ApproveReturnRequestCommandHandler(db, Mock.Of<INotificationNotifier>(), none).Handle(new(1, "note"), default));
        yield return Case("FreezeReturnRequest", db => new FreezeReturnRequestCommandHandler(db, Mock.Of<INotificationNotifier>(), none).Handle(new(1, "note"), default));
        yield return Case("RejectReturnRequest", db => new RejectReturnRequestCommandHandler(db, Mock.Of<INotificationNotifier>(), none).Handle(new(1, "note"), default));
        yield return Case("ResolveProductViolation", db => new ResolveProductViolationCommandHandler(db, Mock.Of<IEmailService>(), Mock.Of<INotificationNotifier>(), none).Handle(new() { ProductId = 1, Action = ViolationResolutionAction.Hide }, default));
        yield return Case("ResolveDispute", db => new ResolveDisputeCommandHandler(db, none, NullLogger<ResolveDisputeCommandHandler>.Instance, Mock.Of<IDisputeNotifier>(), Mock.Of<IEmailService>()).Handle(new() { DisputeId = 1, Winner = "Seller" }, default));
        yield return Case("UpdateDisputePriority", db => new UpdateDisputePriorityCommandHandler(db, none, NullLogger<UpdateDisputePriorityCommandHandler>.Instance).Handle(new(1, "High"), default));
        yield return Case("ApproveWithdrawal", db => new ApproveWithdrawalCommandHandler(db, none, Mock.Of<ISensitiveDataLogger>()).Handle(new(1, "TX"), default));
        yield return Case("RejectWithdrawal", db => new RejectWithdrawalCommandHandler(db, none, Mock.Of<ISensitiveDataLogger>()).Handle(new(1, "reason"), default));

        static TestCaseData Case(string name, Func<ApplicationDbContext, Task> act) => new TestCaseData(act).SetName($"AdminWrite_RejectsMissingActor_{name}");
    }

    [TestCaseSource(nameof(AdminWritesWithoutActor))]
    public async Task AdminWrite_RejectsMissingActorBeforeLookup(Func<ApplicationDbContext, Task> act)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { Id = 7, Role = "SuperAdmin" });
        await db.SaveChangesAsync();

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => act(db));
        Assert.That(await db.AdminActions.CountAsync(), Is.Zero);
    }
}
