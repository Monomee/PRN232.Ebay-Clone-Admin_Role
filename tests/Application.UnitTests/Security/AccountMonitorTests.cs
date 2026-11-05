using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class AccountMonitorTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static (ApplicationDbContext, Func<Task<MonitorResult>>) Setup()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var crypto = EncryptionServiceTests.Create();
        var system = new Mock<IUser>();
        system.SetupGet(x => x.IsSystem).Returns(true); // the background job's actor
        var log = new SensitiveDataLogger(db, crypto, system.Object);
        return (db, async () =>
        {
            await AccountLinking.SyncIdentifiersAsync(db, crypto, db.Users, default);
            return await AccountMonitor.RunAsync(db, crypto, log, Now, default);
        });
    }

    // Seller 2 was fine at sign-up; later it adds the bank account of banned seller 1.
    [Test]
    public async Task ActiveAccount_LinkedToBannedLater_IsRestricted_Once()
    {
        var (db, run) = Setup();
        var crypto = EncryptionServiceTests.Create();
        var bank = crypto.Encrypt(@"{""accountNumber"":""5566778899""}");
        db.Users.AddRange(
            new User { Id = 1, Username = "banned", Status = "Banned", BankAccountMock = bank },
            new User { Id = 2, Username = "seller", BankAccountMock = bank, RiskScore = 0 },
            new User { Id = 3, Username = "innocent" },
            new User { Id = 4, Username = "suspended", Status = "Suspended", BankAccountMock = crypto.Encrypt(@"{""accountNumber"":""1212121212""}") },
            new User { Id = 5, Username = "shares-with-suspended", BankAccountMock = crypto.Encrypt(@"{""accountNumber"":""1212121212""}") });
        await db.SaveChangesAsync();

        Assert.That(await run(), Is.EqualTo(new MonitorResult(0, 1, 0)));
        var seller = await db.Users.SingleAsync(u => u.Id == 2);
        Assert.That(seller.Status, Is.EqualTo("Active"), "never banned automatically");
        Assert.That(seller.ProductBanUntil, Is.GreaterThan(Now));
        Assert.That(seller.PayoutHoldUntil, Is.EqualTo(Now + AccountMonitor.LinkPayoutHold));
        Assert.That(seller.RiskScore, Is.EqualTo(100));

        // Admin lifts it after checking: the same evidence does not restrict again.
        seller.ProductBanUntil = seller.PayoutHoldUntil = null;
        await db.SaveChangesAsync();
        Assert.That(await run(), Is.EqualTo(new MonitorResult(0, 0, 0)));
        Assert.That(seller.PayoutHoldUntil, Is.Null);
    }

    [Test]
    public async Task ListingSpike_ComparedWithOwnHistory()
    {
        var (db, run) = Setup();
        db.Users.AddRange(new User { Id = 1, Username = "burst" }, new User { Id = 2, Username = "busy" });
        Product P(int seller, DateTime at) => new() { SellerId = seller, Title = "x", CreatedAt = at };
        db.Products.AddRange(Enumerable.Range(0, 25).Select(_ => P(1, Now.AddHours(-2))));              // 25 today, nothing before
        db.Products.AddRange(Enumerable.Range(0, 25).Select(_ => P(2, Now.AddHours(-2))));              // 25 today ...
        db.Products.AddRange(Enumerable.Range(0, 600).Select(i => P(2, Now.AddDays(-2 - i % 28))));     // ... but ~20/day usually
        await db.SaveChangesAsync();

        Assert.That(await run(), Is.EqualTo(new MonitorResult(0, 0, 1)));
        Assert.That((await db.Users.SingleAsync(u => u.Id == 1)).ProductBanUntil, Is.EqualTo(Now + AccountMonitor.SpikeRestriction));
        Assert.That((await db.Users.SingleAsync(u => u.Id == 2)).ProductBanUntil, Is.Null);
        Assert.That(await run(), Is.EqualTo(new MonitorResult(0, 0, 0)), "already paused");
    }
}
