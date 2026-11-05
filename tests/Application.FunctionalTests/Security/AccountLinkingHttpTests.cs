using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

public class AccountLinkingHttpTests
{
    // A brand-new seller account is capped: price per item and number of active listings (risky account: tighter).
    [Test]
    public async Task NewSeller_IsLimited_RiskySellerMore()
    {
        using var factory = new Factory();
        factory.EnsureSessionUser(720, "Seller");
        factory.EnsureSessionUser(721, "Seller");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var u in await db.Users.Where(u => u.Id == 720 || u.Id == 721).ToListAsync()) (u.ApprovedAt, u.RiskCheckedAt) = (DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-3));
            (await db.Users.SingleAsync(u => u.Id == 721)).RiskScore = 45; // approved after a manual review
            await db.SaveChangesAsync();
        }
        async Task<int> Post(int seller, decimal price)
        {
            using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
                { BaseAddress = new Uri("https://localhost") };
            client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(seller, "Seller"));
            return (int)(await client.PostAsJsonAsync("/api/products", new { title = "item", price })).StatusCode;
        }

        Assert.That(await Post(720, 600), Is.EqualTo(400), "over the new-account price cap");
        Assert.That(await Post(720, 400), Is.EqualTo(200));
        Assert.That(await Post(721, 400), Is.EqualTo(400), "risky account has a lower cap");
        for (var i = 0; i < SellingLimits.RiskyNewAccount.MaxActiveListings; i++) Assert.That(await Post(721, 50), Is.EqualTo(200));
        Assert.That(await Post(721, 50), Is.EqualTo(400), "listing count cap");
    }

    // Admin undo for the monitor's automatic measures; a seller cannot lift its own.
    [Test]
    public async Task LiftRestrictions_AdminOnly()
    {
        using var factory = new Factory();
        factory.EnsureSessionUser(730, "Seller");
        factory.EnsureSessionUser(701, "SuperAdmin");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seller = await db.Users.SingleAsync(u => u.Id == 730);
            seller.ProductBanUntil = seller.PayoutHoldUntil = DateTime.UtcNow.AddDays(7);
            await db.SaveChangesAsync();
        }
        async Task<int> Lift(int caller, string role)
        {
            using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
                { BaseAddress = new Uri("https://localhost") };
            client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(caller, role));
            return (int)(await client.PostAsync("/api/Users/730/lift-restrictions", null)).StatusCode;
        }

        Assert.That(await Lift(730, "Seller"), Is.EqualTo(403));
        Assert.That(await Lift(701, "SuperAdmin"), Is.EqualTo(204));
        using var check = factory.Services.CreateScope();
        var user = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(u => u.Id == 730);
        Assert.That(user.ProductBanUntil, Is.Null);
        Assert.That(user.PayoutHoldUntil, Is.Null);
    }

    // The admin sees which accounts share data with this one and what kind of data, never the other account's values.
    [Test]
    public async Task UserDetail_ShowsLinkedAccounts_WithoutTheirData()
    {
        using var factory = new Factory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = 710, Username = "banned_seller", Email = "xy@gmail.com", Status = "Banned",
                Addresses = [new Address { Phone = "+84 912 345 678", Street = "1 A" }] });
            db.Users.Add(new User { Id = 711, Username = "new_seller", Email = "x.y+2@gmail.com", ApprovalStatus = "PendingApproval",
                Addresses = [new Address { Phone = "0912345678" }] });
            db.Users.Add(new User { Id = 712, Username = "stranger", Email = "other@gmail.com" });
            await db.SaveChangesAsync();
            await AccountLinking.SyncIdentifiersAsync(db, scope.ServiceProvider.GetRequiredService<IEncryptionService>(), db.Users, default);
        }
        factory.EnsureSessionUser(701, "SuperAdmin");
        using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(701, "SuperAdmin"));

        var response = await client.GetAsync("/api/Users/711");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        var json = JsonDocument.Parse(body).RootElement;
        var linked = json.GetProperty("linkedAccounts").EnumerateArray().ToList();
        Assert.That(linked.Select(l => l.GetProperty("userId").GetInt32()), Is.EqualTo(new[] { 710 }));
        Assert.That(linked[0].GetProperty("sharedTypes").EnumerateArray().Select(t => t.GetString()), Is.EquivalentTo(new[] { "Email", "Phone" }));
        Assert.That(json.GetProperty("currentRiskScore").GetInt32(), Is.EqualTo(120));
        Assert.That(body, Does.Not.Contain("xy@gmail.com").And.Not.Contain("912 345"), "linked account's own data stays hidden");
    }
}
