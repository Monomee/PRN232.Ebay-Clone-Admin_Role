using System.Net;
using System.Net.Http.Json;
using EbayClone.Application.Authentication.Commands.Register;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

public class RegisterHttpTests
{
    // Sign-up over HTTP: the browser is remembered for device linking, the account cannot sign in or be reviewed
    // before the email code, and nobody can sign themselves up as an admin. (Captcha: no key outside Production.)
    [Test]
    public async Task Register_VerifyEmail_ThenWaitsForRiskReview()
    {
        // Buyers/sellers come from public IPs: open InternalIps (test only); the client is a trusted proxy forwarding for 14.161.1.1.
        using var factory = new Factory { Settings = { ["InternalIps:0"] = "0.0.0.0/0", ["InternalIps:1"] = "::/0" } };
        using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "14.161.1.1");

        var response = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "new.seller@example.com", username = "new_seller", password = "secret123", role = "Seller",
            fullName = "Tran Thi B", cccd = "001301001234", dateOfBirth = "2001-05-20", remoteIp = "1.1.1.1",
        });
        Assert.That((int)response.StatusCode, Is.EqualTo(200), await response.Content.ReadAsStringAsync());
        Assert.That(response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("device_id=")), "browser remembered");

        var admin = await client.PostAsJsonAsync("/api/Auth/register", new { email = "x@example.com", username = "xadmin", password = "secret123", role = "SuperAdmin" });
        Assert.That((int)admin.StatusCode, Is.EqualTo(400));

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email = "new.seller@example.com", password = "secret123" });
        Assert.That(await login.Content.ReadAsStringAsync(), Does.Contain("\"success\":false"));

        string code;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "new.seller@example.com");
            Assert.That(user.Status, Is.EqualTo("Unverified"));
            Assert.That(user.LastLoginIp, Is.EqualTo("14.161.1.1"), "from the connection, not the body");
            Assert.That(await db.LoginHistories.CountAsync(h => h.UserId == user.Id && h.DeviceHash != null), Is.EqualTo(1));
            code = EmailOtp.Issue(user, scope.ServiceProvider.GetRequiredService<IEncryptionService>(), DateTime.UtcNow); // the mailed code is only stored hashed
            await db.SaveChangesAsync();
        }

        var verify = await client.PostAsJsonAsync("/api/Auth/verify-email", new { email = "new.seller@example.com", code });
        Assert.That(await verify.Content.ReadAsStringAsync(), Does.Contain("\"success\":true"));
        using var check = factory.Services.CreateScope();
        var verified = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(u => u.Email == "new.seller@example.com");
        Assert.That(verified.Status, Is.EqualTo("Pending"));
        Assert.That(verified.ApprovalStatus, Is.EqualTo("PendingApproval"));
    }
}
