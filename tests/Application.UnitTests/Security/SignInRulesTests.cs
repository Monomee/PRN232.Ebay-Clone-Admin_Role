using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

/// Datacenter IP, impossible travel, device linking and bank-holder name rules.
/// Everything is simulated on one machine: IPs are plain strings, time is a parameter.
public class SignInRulesTests
{
    private const string Hanoi = "14.161.1.1", Comcast = "73.1.1.1", Aws = "3.5.140.2";
    private static readonly DateTime T0 = new(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext Db() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Test]
    public void IpIntelligence_OfflineTables()
    {
        Assert.That(IpIntelligence.DatacenterProvider(Aws), Is.EqualTo("AWS"));
        Assert.That(IpIntelligence.DatacenterProvider(Hanoi), Is.Null, "home ISP");
        Assert.That(IpIntelligence.Locate(Hanoi)!.Place, Does.StartWith("Việt Nam"));
        Assert.That(IpIntelligence.Locate("27.70.1.1")!.Latitude, Is.EqualTo(IpIntelligence.Locate(Hanoi)!.Latitude),
            "two VN ISPs = same place: switching Wi-Fi to 4G is not travel");
        Assert.That(IpIntelligence.DistanceKm(21.03, 105.85, 35.68, 139.69), Is.InRange(3600, 3750)); // Hà Nội - Tokyo
    }

    [TestCase("NGUYEN VAN A", "Nguyễn  Văn A", true)]
    [TestCase("TRAN THI B", "Trần Thị Bình", false)]
    [TestCase("Đặng Văn Đức", "DANG VAN DUC", true)]
    public void BankHolderName_ComparedWithoutDiacritics(string bank, string kyc, bool match) =>
        Assert.That(AccountLinking.NamesMatch(bank, kyc), Is.EqualTo(match));

    [Test]
    public void OwnSignals_Datacenter_And_NameMismatch()
    {
        var crypto = EncryptionServiceTests.Create();
        var user = new User
        {
            LastLoginIp = Aws,
            VerificationDocuments = crypto.Encrypt(@"{""cccd"":""001234567890"",""fullName"":""Nguyễn Văn A""}"),
            BankAccountMock = crypto.Encrypt(@"{""accountNumber"":""0011223344"",""accountName"":""TRAN VAN B""}"),
        };
        var signals = AccountLinking.OwnSignals(user, crypto);
        Assert.That(signals.Sum(s => s.Weight), Is.EqualTo(AccountLinking.DatacenterIpWeight + AccountLinking.NameMismatchWeight));

        user.BankAccountMock = crypto.Encrypt(@"{""accountNumber"":""0011223344"",""accountName"":""NGUYEN VAN A""}");
        user.LastLoginIp = Hanoi;
        Assert.That(AccountLinking.OwnSignals(user, crypto), Is.Empty);
    }

    [Test]
    public async Task ImpossibleTravel_HoldsSellerPayouts_NormalTravelDoesNot()
    {
        using var db = Db();
        db.Users.Add(new User { Id = 1, Username = "seller", Role = "Seller" });
        await db.SaveChangesAsync();

        var first = await SignInMonitor.RecordAsync(db, 1, Hanoi, "dev-a", T0, default);
        Assert.That(first.Flags, Is.Null);

        // Hà Nội -> USA in 10 minutes: not a plane, a second person.
        var jump = await SignInMonitor.RecordAsync(db, 1, Comcast, "dev-b", T0.AddMinutes(10), default);
        Assert.That(jump.Flags, Does.Contain("ImpossibleTravel").And.Contain("NewDevice"));
        var seller = await db.Users.SingleAsync();
        Assert.That(seller.PayoutHoldUntil, Is.EqualTo(T0.AddMinutes(10) + SignInMonitor.TakeoverPayoutHold));
        Assert.That(seller.LastLoginIp, Is.EqualTo(Comcast));
        Assert.That(await db.Notifications.CountAsync(n => n.Title == "New sign-in location"), Is.EqualTo(1));

        // Back home 20 hours later: a real flight is possible.
        var back = await SignInMonitor.RecordAsync(db, 1, Hanoi, "dev-a", T0.AddHours(20), default);
        Assert.That(back.Flags, Is.Null);

        var vps = await SignInMonitor.RecordAsync(db, 1, Aws, "dev-a", T0.AddHours(21), default);
        Assert.That(vps.Flags, Is.EqualTo("Datacenter"));
        Assert.That(vps.Place, Is.EqualTo("AWS"));
    }

    [Test]
    public async Task OldSignIns_AreDeleted_AfterRetention()
    {
        using var db = Db();
        db.LoginHistories.AddRange(
            new LoginHistory { UserId = 1, IpAddress = Hanoi, CreatedAt = T0 - SignInMonitor.Retention - TimeSpan.FromDays(1) },
            new LoginHistory { UserId = 1, IpAddress = Hanoi, CreatedAt = T0.AddDays(-10) });
        await db.SaveChangesAsync();
        Assert.That(await SignInMonitor.PurgeOldAsync(db, T0, default), Is.EqualTo(1));
        Assert.That(await db.LoginHistories.Select(h => h.CreatedAt).SingleAsync(), Is.EqualTo(T0.AddDays(-10)));
    }

    // Two accounts used from the same browser are linked through the device_id cookie hash.
    [Test]
    public async Task SameBrowser_LinksAccounts()
    {
        using var db = Db();
        var crypto = EncryptionServiceTests.Create();
        db.Users.AddRange(new User { Id = 1, Username = "banned", Status = "Banned" }, new User { Id = 2, Username = "new" }, new User { Id = 3, Username = "other" });
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        await SignInMonitor.RecordAsync(db, 1, Hanoi, "browser-x", now.AddDays(-3), default);
        await SignInMonitor.RecordAsync(db, 2, Hanoi, "browser-x", now, default);
        await SignInMonitor.RecordAsync(db, 3, Hanoi, "browser-y", now, default);
        await AccountLinking.SyncIdentifiersAsync(db, crypto, db.Users, default);

        var links = await AccountLinking.FindLinksAsync(db, await db.Users.SingleAsync(u => u.Id == 2), crypto, default);
        Assert.That(links.Where(l => l.Type == "Device").Select(l => (l.UserId, l.Banned)), Is.EqualTo(new[] { (1, true) }));
    }
}
