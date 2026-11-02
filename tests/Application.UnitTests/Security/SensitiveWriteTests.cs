using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Commands.GenerateMockUsers;
using EbayClone.Application.Withdrawals.Commands.RequestWithdrawal;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class SensitiveWriteTests
{
    private ApplicationDbContext _db = null!;
    [SetUp] public void SetUp() => _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [TearDown] public void TearDown() => _db.Dispose();

    [Test]
    public async Task MockUsers_EncryptNationalId_ButKeepDuplicateLookup()
    {
        await new GenerateMockUsersCommandHandler(_db, EncryptionServiceTests.Create()).Handle(new(), default);
        var users = await _db.Users.Where(x => x.CCCD != null).ToListAsync();
        Assert.That(users, Has.Count.EqualTo(2));
        Assert.That(users[0].CCCD, Does.StartWith("gcm:v1:"));
        Assert.That(users[0].CCCD, Is.Not.EqualTo(users[1].CCCD));
        Assert.That(users[0].CccdLookupHash, Is.Not.Null.And.EqualTo(users[1].CccdLookupHash));
    }

    [Test]
    public async Task Withdrawal_StoresEncryptedBankAndAudit_WithoutChangingBalanceRules()
    {
        _db.Users.Add(new User { Id = 42, Role = "Seller", Username = "seller" });
        _db.SellerWallets.Add(new SellerWallet { SellerId = 42, AvailableBalance = 100 });
        await _db.SaveChangesAsync();
        var command = new RequestWithdrawalCommand { Amount = 30, BankName = "VCB", BankAccountName = "Nguyễn An", BankAccountNumber = "001234567890" };
        await new RequestWithdrawalCommandHandler(_db, SensitiveLogTests.Actor("42", "Seller"), EncryptionServiceTests.Create(), new SensitiveDataLogger(_db, EncryptionServiceTests.Create(), SensitiveLogTests.Actor("42", "Seller"))).Handle(command, default);
        var row = await _db.WithdrawalRequests.SingleAsync();
        Assert.That(row.BankAccountNumber, Does.StartWith("gcm:v1:"));
        Assert.That(EncryptionServiceTests.Create().Decrypt(row.BankAccountNumber!), Is.EqualTo("001234567890"));
        Assert.That((await _db.SellerWallets.SingleAsync()).AvailableBalance, Is.EqualTo(70));
        Assert.That((await _db.SellerWallets.SingleAsync()).LockedBalance, Is.EqualTo(30));
        var log = await _db.SensitiveDataLogs.SingleAsync();
        Assert.That(log.ActorId, Is.EqualTo(42));
        Assert.That(log.EncryptedData, Does.Not.Contain("001234567890"));
    }

    [Test]
    public async Task RejectedWithdrawal_DoesNotPersistAuditOrRequest()
    {
        _db.SellerWallets.Add(new SellerWallet { SellerId = 42, AvailableBalance = 10 });
        await _db.SaveChangesAsync();
        var command = new RequestWithdrawalCommand { Amount = 30, BankName = "VCB", BankAccountName = "A", BankAccountNumber = "0012" };
        Assert.ThrowsAsync<InvalidOperationException>(() => new RequestWithdrawalCommandHandler(_db, SensitiveLogTests.Actor("42", "Seller"), EncryptionServiceTests.Create(), new SensitiveDataLogger(_db, EncryptionServiceTests.Create(), SensitiveLogTests.Actor("42", "Seller"))).Handle(command, default));
        Assert.That(await _db.SensitiveDataLogs.CountAsync(), Is.Zero);
        Assert.That(await _db.WithdrawalRequests.CountAsync(), Is.Zero);
    }
}
