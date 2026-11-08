using System.Net;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.SystemAdmin.IpBlocks;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

/// Sign-up blocklist: graduated (shared IPs are innocent), temporary, and an admin lift is not overruled by the job.
public class IpBlocklistTests
{
    private const string Hanoi = "14.161.1.1", Aws = "3.5.140.2";
    private static readonly DateTime Now = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext Db() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Task<int> Run(ApplicationDbContext db, DateTime? at = null) =>
        IpBlocklist.AutoBlockAsync(db, new Mock<ISensitiveDataLogger>().Object, at ?? Now, default);

    private static void Banned(ApplicationDbContext db, int id, string ip, DateTime bannedAt) =>
        db.Users.Add(new User { Id = id, Username = "u" + id, Status = "Banned", BannedAt = bannedAt, LastLoginIp = ip });

    [TestCase("1.2.3.4", "1.2.3.4/32")]
    [TestCase("2001:db8:1:2:aaaa::1", "2001:db8:1:2::/64")] // one IPv6 subscriber holds the whole /64
    [TestCase("1.2.0.0/16", "1.2.0.0/16")]
    [TestCase("10.0.0.0/8", null)]                         // wider than /16: a typo would block a whole provider
    [TestCase("1.2.3.4/33", null)]
    [TestCase("not-an-ip", null)]
    public void ParseAdminInput(string input, string? expected) =>
        Assert.That(IpBlocklist.ParseAdminInput(input)?.ToString(), Is.EqualTo(expected));

    [Test]
    public async Task ThreeRecentBansOnOneIp_BlockSignUp_TwoDoNot()
    {
        using var db = Db();
        Banned(db, 1, Hanoi, Now.AddDays(-1));
        Banned(db, 2, Hanoi, Now.AddDays(-2));
        Banned(db, 3, Hanoi, Now.AddDays(-40)); // outside the 30-day evidence window
        await db.SaveChangesAsync();
        Assert.That(await Run(db), Is.Zero, "2 recent bans: risk score only");

        db.Users.Add(new User { Id = 4, Username = "u4", Status = "Banned", BannedAt = Now.AddDays(-3) });
        db.LoginHistories.Add(new LoginHistory { UserId = 4, IpAddress = Hanoi, CreatedAt = Now.AddDays(-5) }); // via a sign-in
        await db.SaveChangesAsync();
        Assert.That(await Run(db), Is.EqualTo(1));
        Assert.That(await Run(db), Is.Zero, "same evidence: not blocked twice");

        var block = await IpBlocklist.ActiveBlockAsync(db, Hanoi, Now, default);
        Assert.That((block?.Network, block?.IsAutomatic, block?.ExpiresAt), Is.EqualTo(("14.161.1.1/32", true, Now + IpBlocklist.AutoBlockFor)));
        Assert.That(await IpBlocklist.ActiveBlockAsync(db, "14.161.1.2", Now, default), Is.Null, "the neighbour address is not blocked");
        Assert.That(await IpBlocklist.ActiveBlockAsync(db, Hanoi, Now + IpBlocklist.AutoBlockFor, default), Is.Null, "expires on its own");
    }

    [Test]
    public async Task DatacenterIp_OneBanIsEnough()
    {
        using var db = Db();
        Banned(db, 1, Aws, Now.AddDays(-1));
        await db.SaveChangesAsync();
        Assert.That(await Run(db), Is.EqualTo(1));
        Assert.That((await db.IpBlocks.SingleAsync()).Reason, Does.Contain("AWS"));
    }

    // An admin who lifts a block is not overruled by the next run; only a newer ban blocks again.
    [Test]
    public async Task LiftedBlock_StaysLifted_UntilANewBan()
    {
        using var db = Db();
        for (var id = 1; id <= 3; id++) Banned(db, id, Hanoi, Now.AddDays(-id));
        await db.SaveChangesAsync();
        await Run(db);
        var actor = SensitiveLogTests.Actor("99", "SuperAdmin");
        var log = new Mock<ISensitiveDataLogger>().Object;
        Assert.That(await new LiftIpBlockCommandHandler(db, actor, log).Handle(new LiftIpBlockCommand((await db.IpBlocks.SingleAsync()).Id), default), Is.True);
        Assert.That(await IpBlocklist.ActiveBlockAsync(db, Hanoi, Now, default), Is.Null);
        Assert.That(await Run(db, Now.AddHours(1)), Is.Zero);

        Banned(db, 4, Hanoi, Now.AddHours(2));
        await db.SaveChangesAsync();
        Assert.That(await Run(db, Now.AddHours(3)), Is.EqualTo(1));
    }

    [Test]
    public async Task AdminBlock_Ipv6CoversTheSlash64_AndValidatesInput()
    {
        using var db = Db();
        var handler = new BlockIpCommandHandler(db, SensitiveLogTests.Actor("99", "SuperAdmin"), new Mock<ISensitiveDataLogger>().Object);
        await handler.Handle(new BlockIpCommand("2001:db8:1:2::5", "spam", 7), default);
        Assert.That(await IpBlocklist.ActiveBlockAsync(db, "2001:db8:1:2:ffff::9", DateTime.UtcNow, default), Is.Not.Null);
        Assert.That(await IpBlocklist.ActiveBlockAsync(db, "2001:db8:1:3::1", DateTime.UtcNow, default), Is.Null);
        Assert.That((await db.IpBlocks.SingleAsync()).CreatedBy, Is.EqualTo(99));

        var validator = new BlockIpCommandValidator();
        Assert.That(validator.Validate(new BlockIpCommand("0.0.0.0/0", "all", 7)).IsValid, Is.False);
        Assert.That(validator.Validate(new BlockIpCommand("1.2.3.4", "", 7)).IsValid, Is.False);
        Assert.That(validator.Validate(new BlockIpCommand("1.2.3.4", "spam", 0)).IsValid, Is.False);
        Assert.That(validator.Validate(new BlockIpCommand("1.2.3.4", "spam", 30)).IsValid, Is.True);
    }
}
