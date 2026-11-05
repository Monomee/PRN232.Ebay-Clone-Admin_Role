using System.Net;
using System.Net.Http.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

/// One machine, many "places": the client connects from loopback (a trusted proxy) and sends X-Forwarded-For,
/// exactly how nginx reports the real client IP in production.
public class SignInRulesHttpTests
{
    // This app normally only answers internal IPs (InternalIpMiddleware); these rules are about customer sign-ins,
    // so the tests open it the way a buyer/seller-facing deployment would.
    private static Factory PublicFactory() => new() { Settings = { ["InternalIps:0"] = "0.0.0.0/0", ["InternalIps:1"] = "::/0" } };

    private static HttpClient Client(Factory factory, string forwardedFor)
    {
        var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    [Test]
    public async Task SignIns_AreRecorded_WithRealIp_DeviceCookie_AndImpossibleTravel()
    {
        using var factory = PublicFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = 740, Email = "traveller@test.local", Username = "traveller", Role = "Seller",
                Password = BCrypt.Net.BCrypt.HashPassword("Passw0rd!") });
            await db.SaveChangesAsync();
        }

        // Sign-in 1 from Hà Nội: the server hands out a device cookie.
        using var hanoi = Client(factory, "14.161.1.1");
        var first = await hanoi.PostAsJsonAsync("/api/Auth/login", new { email = "traveller@test.local", password = "Passw0rd!" });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var deviceCookie = first.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("device_id="));
        Assert.That(deviceCookie, Does.Contain("httponly").IgnoreCase.And.Contain("secure").IgnoreCase);

        // Sign-in 2, same browser (cookie sent back), seconds later from the USA.
        using var usa = Client(factory, "73.1.1.1");
        usa.DefaultRequestHeaders.Add("Cookie", deviceCookie.Split(';')[0]);
        var second = await usa.PostAsJsonAsync("/api/Auth/login", new { email = "traveller@test.local", password = "Passw0rd!" });
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(second.Headers.TryGetValues("Set-Cookie", out var again) && again.Any(c => c.StartsWith("device_id=")), Is.False,
            "known browser keeps its id");

        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var history = await context.LoginHistories.Where(h => h.UserId == 740).OrderBy(h => h.Id).ToListAsync();
        Assert.That(history.Select(h => h.IpAddress), Is.EqualTo(new[] { "14.161.1.1", "73.1.1.1" }));
        Assert.That(history[0].DeviceHash, Is.EqualTo(history[1].DeviceHash));
        Assert.That(history[1].Flags, Is.EqualTo("ImpossibleTravel"));
        var user = await context.Users.SingleAsync(u => u.Id == 740);
        Assert.That(user.LastLoginIp, Is.EqualTo("73.1.1.1"));
        Assert.That(user.PayoutHoldUntil, Is.GreaterThan(DateTime.UtcNow.AddHours(47)));
    }

    [Test]
    public async Task BankAccountChange_MustMatchKycName_AndHoldsPayouts()
    {
        using var factory = PublicFactory();
        factory.EnsureSessionUser(750, "Seller");
        using (var scope = factory.Services.CreateScope())
        {
            var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seller = await db.Users.SingleAsync(u => u.Id == 750);
            seller.VerificationDocuments = crypto.Encrypt(@"{""fullName"":""Nguyễn Văn A""}");
            seller.BankAccountMock = crypto.Encrypt(@"{""bankName"":""VCB"",""accountNumber"":""0011223344"",""accountName"":""NGUYEN VAN A""}");
            await db.SaveChangesAsync();
        }
        using var client = Client(factory, "14.161.1.1");
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(750, "Seller"));

        var someoneElse = await client.PutAsJsonAsync("/api/Account/bank-account",
            new { bankName = "TCB", accountNumber = "9988776655", accountName = "TRAN VAN B" });
        Assert.That(someoneElse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await someoneElse.Content.ReadAsStringAsync());

        var own = await client.PutAsJsonAsync("/api/Account/bank-account",
            new { bankName = "TCB", accountNumber = "9988-7766-55", accountName = "nguyen van a" });
        Assert.That(own.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), await own.Content.ReadAsStringAsync());

        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.SingleAsync(u => u.Id == 750);
        Assert.That(user.PayoutHoldUntil, Is.InRange(DateTime.UtcNow.AddHours(47), DateTime.UtcNow.AddHours(49)));
        Assert.That(await context.UserIdentifiers.CountAsync(i => i.UserId == 750 && i.Type == "Bank"), Is.EqualTo(1), "linked right away");
        Assert.That(await context.SensitiveDataLogs.CountAsync(l => l.Action == "UpdatePaymentInfo"), Is.EqualTo(1));

        // Admins have no bank account to change here.
        using var admin = Client(factory, "14.161.1.1");
        factory.EnsureSessionUser(751, "SuperAdmin");
        admin.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(751, "SuperAdmin"));
        Assert.That((await admin.PutAsJsonAsync("/api/Account/bank-account", new { bankName = "x", accountNumber = "123456", accountName = "x" })).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
