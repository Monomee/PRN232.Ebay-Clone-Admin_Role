using EbayClone.Application.AdminRoles.Commands.AssignRole;
using EbayClone.Application.AdminRoles.Commands.CreateAdminUser;
using EbayClone.Application.Common.Exceptions;
using EbayClone.Application.Payouts.Queries.GetPayoutExceptions;
using EbayClone.Application.Users.Queries.GetUserById;
using EbayClone.Application.Withdrawals.Queries.GetWithdrawalRequests;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Data.Seeders;
using EbayClone.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class SensitiveReadAndSeedTests
{
    private ApplicationDbContext _db = null!;
    [SetUp] public void SetUp() => _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [TearDown] public void TearDown() => _db.Dispose();
    private SensitiveDataLogger Logger() => new(_db, EncryptionServiceTests.Create(), SensitiveLogTests.Actor("42", "SuperAdmin"));

    [Test]
    public async Task UserProfile_DecryptsOnlyForMasking_AndPersistsReadAudit()
    {
        var crypto = EncryptionServiceTests.Create();
        _db.Users.Add(new User { Id = 1, Username = "fixture", VerificationDocuments = crypto.Encrypt("{\"cccd\":\"001234567890\"}") });
        await _db.SaveChangesAsync();
        var handler = new GetUserByIdQueryHandler(_db, SensitiveLogTests.Actor("42", "Support"), crypto, Logger());
        var dto = await handler.Handle(new(1), default);
        Assert.That(dto!.MaskedNationalId, Is.EqualTo("00********90"));
        Assert.That((await _db.SensitiveDataLogs.SingleAsync()).Action, Is.EqualTo("ViewUserProfile"));
        var denied = new GetUserByIdQueryHandler(_db, SensitiveLogTests.Actor("42", "Monitor"), crypto, Logger());
        Assert.ThrowsAsync<ForbiddenAccessException>(() => denied.Handle(new(1), default));
    }

    [Test]
    public async Task FinancialLists_DoNotExposeBankPlaintextOrCiphertext()
    {
        var crypto = EncryptionServiceTests.Create();
        _db.Users.Add(new User { Id = 1, Username = "fixture" });
        _db.WithdrawalRequests.Add(new WithdrawalRequest { SellerId = 1, BankName = crypto.Encrypt("VCB"), BankAccountNumber = crypto.Encrypt("001234567890"), BankAccountName = crypto.Encrypt("Fixture") });
        _db.PayoutTransactions.Add(new PayoutTransaction { SellerId = 1, Status = "Failed", BankSnapshot = crypto.Encrypt("{\"accountNumber\":\"001234567890\"}") });
        await _db.SaveChangesAsync();
        var requests = await new GetWithdrawalRequestsQueryHandler(_db).Handle(new(null), default);
        Assert.That(requests.Single().BankAccountNumber, Is.EqualTo("********"));
        Assert.That(requests.Single().BankAccountName, Is.EqualTo("[Protected]"));
        var payouts = await new GetPayoutExceptionsQueryHandler(_db).Handle(new(), default);
        Assert.That(payouts.Single().BankSnapshot, Is.EqualTo("[Protected bank details]"));
    }

    [Test]
    public async Task AdminCreationAndRoleChange_AuditDoesNotContainPassword()
    {
        _db.AdminRoles.Add(new AdminRole { Id = 1, RoleName = "Support" });
        await _db.SaveChangesAsync();
        var create = new CreateAdminUserCommandHandler(_db, Logger(), SensitiveLogTests.Actor("42", "SuperAdmin"));
        var id = await create.Handle(new() { Username = "new-admin", Email = "fixture@example.test", Password = "private-password", RoleId = 1, CreatedBy = 999 }, default);
        var user = await _db.Users.FindAsync(id);
        Assert.That(BCrypt.Net.BCrypt.Verify("private-password", user!.Password), Is.True);
        await new AssignRoleCommandHandler(_db, Logger(), SensitiveLogTests.Actor("42", "SuperAdmin")).Handle(new() { UserId = id, RoleId = 1, AssignedBy = 999 }, default);
        Assert.That(await _db.AdminActions.AllAsync(x => x.AdminId == 42), Is.True);
        Assert.That(await _db.AdminUserRoles.AllAsync(x => x.AssignedBy == 42), Is.True);
        var logs = await _db.SensitiveDataLogs.ToListAsync();
        Assert.That(logs.Select(x => x.Action), Does.Contain("AssignAdminRole"));
        foreach (var log in logs)
        {
            Assert.That(log.ActorId, Is.EqualTo(42));
            Assert.That(EncryptionServiceTests.Create().Decrypt(log.EncryptedData), Does.Not.Contain("private-password"));
        }
    }

    [Test]
    public async Task Seeders_UseInjectedKey_AndStoreOnlyEncryptedBankAndLogPayloads()
    {
        var crypto = EncryptionServiceTests.Create();
        await new PayoutEngineDemoSeeder(_db, NullLogger<PayoutEngineDemoSeeder>.Instance, crypto).SeedAsync();
        await new SensitiveDataLogsSeeder(_db, NullLogger.Instance, crypto).SeedAsync();
        var banks = await _db.Users.Where(x => x.BankAccountMock != null).Select(x => x.BankAccountMock!).ToListAsync();
        var snapshots = await _db.PayoutTransactions.Where(x => x.BankSnapshot != null).Select(x => x.BankSnapshot!).ToListAsync();
        Assert.That(banks, Is.Not.Empty);
        Assert.That(snapshots, Is.Not.Empty);
        foreach (var value in banks.Concat(snapshots))
        {
            Assert.That(value, Does.StartWith("gcm:v1:"));
            Assert.That(crypto.Decrypt(value), Does.Contain("accountNumber"));
        }
        var logs = await _db.SensitiveDataLogs.ToListAsync();
        Assert.That(logs, Is.Not.Empty);
        Assert.That(logs.All(x => crypto.VerifyIntegrity(crypto.Decrypt(x.EncryptedData), x.DataHash, x.EncryptionAlgorithm)), Is.True);
        foreach (var log in logs)
        {
            using var json = System.Text.Json.JsonDocument.Parse(crypto.Decrypt(log.EncryptedData));
            foreach (var key in new[] { "secretKey", "recoveryCodes", "temporaryPasswordHash", "password", "otp", "token" })
                Assert.That(json.RootElement.TryGetProperty(key, out _), Is.False);
        }
    }
}
