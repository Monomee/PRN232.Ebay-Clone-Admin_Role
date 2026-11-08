using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Commands.UpdateBankAccount;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
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

    // Shapes of the real feeds (trimmed). EC2 only from AWS: Amazon's own retail ranges are not rented servers.
    private const string AwsFeed = """
        { "prefixes": [ { "ip_prefix": "3.5.140.0/22", "service": "AMAZON" }, { "ip_prefix": "3.5.140.0/22", "service": "EC2" },
                        { "ip_prefix": "54.239.0.0/16", "service": "AMAZON" } ],
          "ipv6_prefixes": [ { "ipv6_prefix": "2600:1f00::/24", "service": "EC2" } ] }
        """;
    private const string GoogleFeed = """
        { "prefixes": [ { "ipv4Prefix": "34.1.208.0/20", "service": "Google Cloud" }, { "ipv6Prefix": "2600:1900::/35", "service": "Google Cloud" } ] }
        """;

    [Test]
    public void DatacenterRanges_DownloadedFeedsReplaceTheSample_PerProvider()
    {
        var aws = IpIntelligence.ParseAwsRanges(AwsFeed);
        Assert.That(aws.Select(r => r.Item1.ToString()), Is.EquivalentTo(new[] { "3.5.140.0/22", "2600:1f00::/24" }));
        var google = IpIntelligence.ParseGoogleCloudRanges(GoogleFeed);
        Assert.That(google, Has.Count.EqualTo(2));

        try
        {
            IpIntelligence.UseDatacenterRanges(aws);
            Assert.That(IpIntelligence.DatacenterProvider("2600:1f00::1"), Is.EqualTo("AWS"));
            Assert.That(IpIntelligence.DatacenterProvider("52.1.1.1"), Is.Null, "AWS sample row replaced by the feed");
            Assert.That(IpIntelligence.DatacenterProvider("54.239.1.1"), Is.Null, "Amazon retail is not a datacenter");
            Assert.That(IpIntelligence.DatacenterProvider("35.192.0.1"), Is.EqualTo("Google Cloud"), "no Google feed yet: sample kept");

            IpIntelligence.UseDatacenterRanges(google); // later refresh where only Google answered
            Assert.That(IpIntelligence.DatacenterProvider("34.1.208.9"), Is.EqualTo("Google Cloud"));
            Assert.That(IpIntelligence.DatacenterProvider("35.192.0.1"), Is.Null);
            Assert.That(IpIntelligence.DatacenterProvider("2600:1f00::1"), Is.EqualTo("AWS"), "earlier AWS download kept");
        }
        finally
        {
            // back to the sample rows other tests rely on
            IpIntelligence.UseDatacenterRanges([
                (System.Net.IPNetwork.Parse("3.0.0.0/8"), "AWS"), (System.Net.IPNetwork.Parse("52.0.0.0/10"), "AWS"),
                (System.Net.IPNetwork.Parse("34.64.0.0/10"), "Google Cloud"), (System.Net.IPNetwork.Parse("35.192.0.0/12"), "Google Cloud")]);
        }
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
        db.Users.Add(new User { Id = 1, Username = "seller", Role = "Seller", Email = "seller@example.com" });
        await db.SaveChangesAsync();
        var mail = new Mock<IEmailService>();

        var first = await SignInMonitor.RecordAsync(db, 1, Hanoi, "dev-a", T0, default, mail.Object);
        Assert.That(first.Flags, Is.Null);

        // Hà Nội -> USA in 10 minutes: not a plane, a second person.
        var jump = await SignInMonitor.RecordAsync(db, 1, Comcast, "dev-b", T0.AddMinutes(10), default, mail.Object);
        Assert.That(jump.Flags, Does.Contain("ImpossibleTravel").And.Contain("NewDevice"));
        var seller = await db.Users.SingleAsync();
        Assert.That(seller.PayoutHoldUntil, Is.EqualTo(T0.AddMinutes(10) + SignInMonitor.TakeoverPayoutHold));
        Assert.That(seller.LastLoginIp, Is.EqualTo(Comcast));
        Assert.That(await db.Notifications.CountAsync(n => n.Title == "New sign-in location"), Is.EqualTo(1));
        // The owner's mailbox too: whoever took the account over can dismiss the in-app copy.
        mail.Verify(m => m.SendEmailAsync("seller@example.com", It.Is<string>(t => t.Contains(SignInMonitor.NewLocationTitle)),
            It.Is<string>(b => b.Contains("United States"))), Times.Once);

        // Back home 20 hours later: a real flight is possible.
        var back = await SignInMonitor.RecordAsync(db, 1, Hanoi, "dev-a", T0.AddHours(20), default, mail.Object);
        Assert.That(back.Flags, Is.Null);

        var vps = await SignInMonitor.RecordAsync(db, 1, Aws, "dev-a", T0.AddHours(21), default);
        Assert.That(vps.Flags, Is.EqualTo("Datacenter"));
        Assert.That(vps.Place, Is.EqualTo("AWS"));
        mail.Verify(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once, "normal sign-ins send nothing");
    }

    // Changing where payouts go is mailed to the owner; a mail server outage never undoes the (already saved) change.
    [TestCase(false)]
    [TestCase(true)]
    public async Task BankAccountChange_MailsTheOwner_MailFailureKeepsTheChange(bool mailDown)
    {
        using var db = Db();
        db.Users.Add(new User { Id = 1, Username = "seller", Role = "Seller", Email = "seller@example.com" });
        await db.SaveChangesAsync();
        var crypto = EncryptionServiceTests.Create();
        var actor = SensitiveLogTests.Actor("1", "Seller");
        var mail = new Mock<IEmailService>();
        if (mailDown) mail.Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("smtp"));

        await new UpdateBankAccountCommandHandler(db, actor, crypto, new EbayClone.Infrastructure.Services.SensitiveDataLogger(db, crypto, actor), mail.Object)
            .Handle(new UpdateBankAccountCommand("VCB", "0011223344", "Seller"), default);

        mail.Verify(m => m.SendEmailAsync("seller@example.com", It.Is<string>(t => t.Contains(UpdateBankAccountCommandHandler.BankChangedTitle)),
            It.IsAny<string>()), Times.Once);
        Assert.That((await db.Users.SingleAsync()).BankAccountMock, Is.Not.Null);
        Assert.That(await db.Notifications.CountAsync(n => n.Title == UpdateBankAccountCommandHandler.BankChangedTitle), Is.EqualTo(1));
    }

    // City-level data puts 4G in Saigon at a Hanoi hub: same country is never travel, another country still is.
    [Test]
    public void ImpossibleTravel_NeverWithinOneCountry()
    {
        var hanoi = new LoginHistory { IpAddress = Hanoi, Latitude = 21.03, Longitude = 105.85, CreatedAt = T0 };
        Assert.That(SignInMonitor.IsImpossibleTravel(hanoi, new IpLocation(10.82, 106.63, "Ho Chi Minh City, Vietnam", "VN"), T0.AddMinutes(5)), Is.False);
        Assert.That(SignInMonitor.IsImpossibleTravel(hanoi, new IpLocation(39.83, -98.58, "Kansas, United States", "US"), T0.AddMinutes(5)), Is.True);
    }

    // The real database file (data/geoip, downloaded, not committed): city-level answers with the country code.
    [Test]
    public void GeoIpDatabase_LocatesCityWithCountry()
    {
        var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../data/geoip/dbip-city-lite.mmdb"));
        if (!File.Exists(path)) Assert.Ignore($"{path} not downloaded");
        try
        {
            EbayClone.Infrastructure.Services.GeoIpLocator.Configure(path);
            var vn = IpIntelligence.Locate(Hanoi)!;
            Assert.That((vn.Country, vn.Place.Contains(',')), Is.EqualTo(("VN", true)), vn.Place);
            Assert.That(IpIntelligence.Locate("8.8.8.8")!.Country, Is.EqualTo("US"));
            Assert.That(IpIntelligence.Locate("10.0.0.1"), Is.Null);
        }
        finally { IpIntelligence.UseLocator(null); }
    }

    // One bank account, one seller: a second account pointing payouts at it is refused and the attempt is logged.
    [Test]
    public async Task BankAccount_UsedByAnotherAccount_IsRefused()
    {
        using var db = Db();
        var crypto = EncryptionServiceTests.Create();
        db.Users.AddRange(new User { Id = 1, Username = "a", Role = "Seller", Email = "a@example.com" },
            new User { Id = 2, Username = "b", Role = "Seller", Email = "b@example.com" });
        await db.SaveChangesAsync();
        UpdateBankAccountCommandHandler Handler(string id)
        {
            var actor = SensitiveLogTests.Actor(id, "Seller");
            return new(db, actor, crypto, new EbayClone.Infrastructure.Services.SensitiveDataLogger(db, crypto, actor), new Mock<IEmailService>().Object);
        }

        await Handler("1").Handle(new UpdateBankAccountCommand("VCB", "0011223344", "A"), default);
        Assert.ThrowsAsync<EbayClone.Application.Common.Exceptions.ValidationException>(() =>
            Handler("2").Handle(new UpdateBankAccountCommand("VCB", "0011-2233-44", "B"), default));
        Assert.That((await db.Users.SingleAsync(u => u.Id == 2)).BankAccountMock, Is.Null);
        Assert.That(await db.SensitiveDataLogs.CountAsync(l => l.Action == "BankAccountReuse"), Is.EqualTo(1));

        await Handler("1").Handle(new UpdateBankAccountCommand("VCB", "0011223344", "A"), default); // own account again: fine
    }

    // Farming with private windows (new device cookie each time) from one network still adds up.
    [Test]
    public async Task SignupBurst_FromOnePublicIp_SendsToReview()
    {
        using var db = Db();
        var crypto = EncryptionServiceTests.Create();
        var t = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 4; i++)
            db.Users.Add(new User { Id = i, Username = "u" + i, LastLoginIp = Hanoi, CreatedAt = t.AddMinutes(i) });
        db.Users.Add(new User { Id = 9, Username = "dev", LastLoginIp = "172.18.0.1", CreatedAt = t.AddMinutes(9) });
        db.Users.Add(new User { Id = 10, Username = "old", LastLoginIp = Hanoi, CreatedAt = t.AddDays(-3) });
        await db.SaveChangesAsync();

        async Task<int> Burst(int id) =>
            (await AccountLinking.OwnSignalsAsync(db, await db.Users.SingleAsync(u => u.Id == id), crypto, default)).Sum(s => s.Weight);
        Assert.That(await Burst(3), Is.Zero, "2 earlier sign-ups: under the bar");
        Assert.That(await Burst(4), Is.EqualTo(AccountLinking.Options.SignupBurstWeight), "3 earlier in 24h");
        Assert.That(await Burst(9), Is.Zero, "private IP is our proxy, never counted");
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
