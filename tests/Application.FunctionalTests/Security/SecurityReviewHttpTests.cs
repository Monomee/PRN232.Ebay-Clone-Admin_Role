using System.Net;
using System.Net.Http.Json;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Web.Endpoints;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

/// Findings of the 2026-11 security review: hub access, test tooling in Production, per-account lockout,
/// JWT signing key, error details.
public class SecurityReviewHttpTests
{
    private static HttpClient Client(Factory factory, string? forwardedFor = null)
    {
        var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        if (forwardedFor != null) client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static HttpClient As(Factory factory, int userId, string role)
    {
        factory.EnsureSessionUser(userId, role);
        var client = Client(factory);
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(userId, role));
        return client;
    }

    // Every connection to these hubs joins the "Admins" broadcast group: a seller must not even connect.
    [TestCase("/hubs/dispute")]
    [TestCase("/hubs/seller")]
    public async Task AdminHubs_RefuseSellers(string hub)
    {
        using var factory = new Factory();
        using var seller = As(factory, 801, "Seller");
        using var admin = As(factory, 802, "SuperAdmin");
        Assert.That((int)(await seller.PostAsync(hub + "/negotiate?negotiateVersion=1", null)).StatusCode, Is.EqualTo(403));
        Assert.That((int)(await admin.PostAsync(hub + "/negotiate?negotiateVersion=1", null)).StatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task Mocking_MoneyEndpointsSuperAdminOnly_AndSwitchedOffByConfig()
    {
        using (var factory = new Factory())
        {
            using var support = As(factory, 811, "Support"); // has ManageUsers
            Assert.That((int)(await support.PostAsJsonAsync("/api/Mocking/push-payout", new { sellerId = 1, amount = 1 })).StatusCode, Is.EqualTo(403));
            Assert.That((int)(await support.PostAsJsonAsync("/api/Mocking/accelerate-settlement", new { })).StatusCode, Is.EqualTo(403));
        }
        using (var off = new Factory { Settings = { ["Features:MockingEndpoints"] = "false" } })
        {
            using var admin = As(off, 812, "SuperAdmin");
            // Not mapped at all; the SPA fallback (GET only) answers a POST with 405.
            Assert.That((int)(await admin.PostAsync("/api/Mocking/generate-mock-users", null)).StatusCode, Is.AnyOf(404, 405));
        }
    }

    [Test]
    public async Task Swagger_SwitchedOffByConfig()
    {
        using (var on = new Factory())
        using (var client = Client(on))
            Assert.That(await client.GetStringAsync("/swagger/v1/swagger.json"), Does.Contain("EbayClone API"));
        using (var off = new Factory { Settings = { ["Features:Swagger"] = "false" } })
        using (var client = Client(off))
        {
            var response = await client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("EbayClone API"));
        }
    }

    [Test]
    public void Mocking_OffInProductionByDefault()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        Assert.That(Mocking.Enabled(config, new HostingEnvironment { EnvironmentName = Environments.Production }), Is.False);
        Assert.That(Mocking.Enabled(config, new HostingEnvironment { EnvironmentName = Environments.Development }), Is.True);
    }

    // Guessing one account's password from many IPs (each under the per-IP limit) stops at the per-account lock,
    // and once locked even the right password waits.
    [Test]
    public async Task Login_LocksAccountAfterRepeatedFailures_FromAnyIp()
    {
        using var factory = new Factory { Settings = { ["InternalIps:0"] = "0.0.0.0/0", ["InternalIps:1"] = "::/0" } };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = 821, Email = "victim@example.com", Username = "victim", Role = "SuperAdmin", Password = BCrypt.Net.BCrypt.HashPassword("right-password1") });
            await db.SaveChangesAsync();
        }
        async Task<string> Login(string password, int ip)
        {
            using var client = Client(factory, $"203.0.113.{ip}");
            var body = await (await client.PostAsJsonAsync("/api/Auth/login", new { email = "victim@example.com", password })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return body.GetProperty("errorMessage").GetString() ?? "";
        }

        for (var i = 0; i < Auth.MaxPasswordFailures; i++)
            Assert.That(await Login("wrong", i + 1), Does.Contain("không đúng"));
        Assert.That(await Login("right-password1", 99), Does.Contain("tạm thời bị khóa"));
        Assert.That(await Login("wrong", 98), Does.Contain("tạm thời bị khóa"), "an unknown or known email answers the same once locked");
    }

    [Test]
    public void JwtKey_ShortOrSampleKeyRefused()
    {
        var production = new HostingEnvironment { EnvironmentName = Environments.Production };
        var development = new HostingEnvironment { EnvironmentName = Environments.Development };
        Assert.Throws<InvalidOperationException>(() => JwtKeyPolicy.Ensure("too-short", development));
        Assert.Throws<InvalidOperationException>(() => JwtKeyPolicy.Ensure(null, development));
        const string sample = "YourSuperSecretJwtKeyAtLeast32Chars!"; // .env.example
        Assert.Throws<InvalidOperationException>(() => JwtKeyPolicy.Ensure(sample, production));
        Assert.DoesNotThrow(() => JwtKeyPolicy.Ensure(sample, development));
        Assert.DoesNotThrow(() => JwtKeyPolicy.Ensure(Convert.ToBase64String(new byte[64]), production));
    }

    // A database or null-reference error must not reach the client with its message; a rule message still does.
    [Test]
    public async Task UnexpectedException_Returns500WithoutDetails()
    {
        var handler = new CustomExceptionHandler(NullLogger<CustomExceptionHandler>.Instance);
        async Task<(int Status, string Body)> Handle(Exception ex)
        {
            var http = new DefaultHttpContext();
            http.Response.Body = new MemoryStream();
            await handler.TryHandleAsync(http, ex, default);
            http.Response.Body.Position = 0;
            return (http.Response.StatusCode, await new StreamReader(http.Response.Body).ReadToEndAsync());
        }

        static string Title(string body) => System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("title").GetString()!;

        var (status, body) = await Handle(new InvalidCastException("Column 'password' of table [User] ..."));
        Assert.That(status, Is.EqualTo(500));
        Assert.That(body, Does.Not.Contain("password").And.Not.Contain("[User]"));

        (status, body) = await Handle(new Exception("Tài khoản của bạn đã bị khóa."));
        Assert.That(status, Is.EqualTo(400));
        Assert.That(Title(body), Is.EqualTo("Tài khoản của bạn đã bị khóa."));
    }
}
