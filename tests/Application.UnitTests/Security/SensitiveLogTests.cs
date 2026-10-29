using System.Security.Cryptography;
using EbayClone.Application.Common.Exceptions;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.SensitiveLogs.Queries.DecryptSensitiveLog;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class SensitiveLogTests
{
    private ApplicationDbContext _db = null!;

    [SetUp] public void SetUp() => _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [TearDown] public void TearDown() => _db.Dispose();

    internal static IUser Actor(string? id = "42", params string[] roles)
    {
        var actor = new Mock<IUser>();
        actor.SetupGet(x => x.Id).Returns(id);
        actor.SetupGet(x => x.Name).Returns("operator@example.test");
        actor.SetupGet(x => x.Roles).Returns(roles.ToList());
        return actor.Object;
    }

    private async Task<SensitiveDataLog> Seed(string? hash = null)
    {
        var crypto = EncryptionServiceTests.Create();
        var row = new SensitiveDataLog { Action = "BanUser", TargetEntity = "User", ActorId = 42,
            EncryptionAlgorithm = "AES-256-GCM", EncryptedData = crypto.Encrypt("{\"reason\":\"private\"}"),
            DataHash = hash ?? crypto.ComputeHmac("{\"reason\":\"private\"}") };
        _db.SensitiveDataLogs.Add(row);
        await _db.SaveChangesAsync();
        return row;
    }

    [Test]
    public async Task Logger_AddDoesNotCommit_AndRejectsMissingRequestActor()
    {
        var crypto = EncryptionServiceTests.Create();
        var logger = new SensitiveDataLogger(_db, crypto, Actor("42", "Support"));
        logger.Add("BanUser", "User", "1", "PII", new { reason = "private" });
        Assert.That(await _db.SensitiveDataLogs.CountAsync(), Is.Zero);
        Assert.That(_db.ChangeTracker.Entries<SensitiveDataLog>().Single().Entity.ActorId, Is.EqualTo(42));
        Assert.Throws<UnauthorizedAccessException>(() => new SensitiveDataLogger(_db, crypto, Actor(null))
            .Add("BanUser", "User", "1", "PII", new { reason = "private" }));
        await _db.SaveChangesAsync();
        var row = await _db.SensitiveDataLogs.SingleAsync();
        Assert.That(row.ActorUsername, Is.EqualTo("operator@example.test"));
        Assert.That(crypto.Decrypt(row.EncryptedData), Is.EqualTo("{\"reason\":\"private\"}"));
    }

    [Test]
    public async Task AuditSaveFailure_PreventsPlaintextResponse()
    {
        var row = await Seed();
        var failedContext = new Mock<IApplicationDbContext>();
        failedContext.SetupGet(x => x.SensitiveDataLogs).Returns(_db.SensitiveDataLogs);
        failedContext.SetupGet(x => x.AdminActions).Returns(_db.AdminActions);
        failedContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Database unavailable"));
        var handler = new DecryptSensitiveLogQueryHandler(failedContext.Object, EncryptionServiceTests.Create(), Actor("42", "SuperAdmin"));
        Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(row.Id), default));
        Assert.That(await _db.AdminActions.CountAsync(), Is.Zero);
    }

    [TestCase("Support")]
    [TestCase("Monitor")]
    [TestCase("Seller")]
    public async Task Decrypt_RequiresSuperAdmin_EvenWhenHandlerCalledDirectly(string role)
    {
        var row = await Seed();
        var handler = new DecryptSensitiveLogQueryHandler(_db, EncryptionServiceTests.Create(), Actor("42", role));
        Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new(row.Id), default));
        Assert.That(await _db.AdminActions.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task InvalidIntegrity_DoesNotReturnPlaintextOrCommitSuccessAudit()
    {
        var row = await Seed(new string('0', 64));
        var handler = new DecryptSensitiveLogQueryHandler(_db, EncryptionServiceTests.Create(), Actor("42", "SuperAdmin"));
        Assert.CatchAsync<CryptographicException>(() => handler.Handle(new(row.Id), default));
        Assert.That(await _db.AdminActions.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task SuccessfulDecrypt_PersistsAuditWithAuthenticatedActor()
    {
        var row = await Seed();
        var handler = new DecryptSensitiveLogQueryHandler(_db, EncryptionServiceTests.Create(), Actor("42", "SuperAdmin"));
        var result = await handler.Handle(new(row.Id), default);
        Assert.That(result!.DecryptedData, Is.EqualTo("{\"reason\":\"private\"}"));
        Assert.That(result.IntegrityVerified, Is.True);
        Assert.That((await _db.AdminActions.SingleAsync()).AdminId, Is.EqualTo(42));
    }

    [Test]
    public async Task MissingActor_DoesNotImpersonateAdminOne()
    {
        var row = await Seed();
        var handler = new DecryptSensitiveLogQueryHandler(_db, EncryptionServiceTests.Create(), Actor(null, "SuperAdmin"));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(row.Id), default));
    }
}
