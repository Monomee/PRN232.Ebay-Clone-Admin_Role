using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

/// Regressions for the security review fixes (spoofed client IP, anonymous writes, 2FA binding, rate limits, sessions).
public class SecurityHardeningHttpTests
{
    private static Factory _factory = null!;

    [OneTimeSetUp] public void Start() => _factory = new Factory();
    [OneTimeTearDown] public void Stop() => _factory.Dispose();

    // A client hitting Kestrel directly must not pass the internal-IP allowlist by claiming to be localhost.
    [Test]
    public async Task SpoofedForwardedFor_FromUntrustedPeer_IsRejected()
    {
        using var client = Client(IPAddress.Parse("203.0.113.7"));
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
        Assert.That((int)(await client.GetAsync("/api/Auth/me")).StatusCode, Is.EqualTo(403));
    }

    // Through our own proxy the real (public) client address is what the allowlist sees.
    [Test]
    public async Task ForwardedFor_FromTrustedProxy_UsesRealClientIp()
    {
        using var client = Client();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");
        Assert.That((int)(await client.GetAsync("/api/Auth/me")).StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task CreateProduct_Anonymous_IsRejected()
    {
        using var client = Client();
        Assert.That((int)(await client.PostAsJsonAsync("/api/products", new { sellerId = 7 })).StatusCode, Is.EqualTo(401));
    }

    // The seller is the caller from the JWT, not the sellerId in the body.
    [Test]
    public async Task CreateProduct_IgnoresSellerIdFromBody()
    {
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(42, "Seller"));
        var response = await client.PostAsJsonAsync("/api/products", new { sellerId = 999, description = "spoof-check" });
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        using var scope = _factory.Services.CreateScope();
        var product = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Products.SingleAsync(p => p.Description == "spoof-check");
        Assert.That(product.SellerId, Is.EqualTo(42));
    }

    // Knowing a userId and its TOTP code is not enough: the password step must have issued the challenge.
    [Test]
    public async Task Verify2FA_WithoutPasswordChallenge_IsRejected()
    {
        var (user, secret) = await SeedTwoFactorUser("nochallenge@test.local");
        using var client = Client();
        var response = await client.PostAsJsonAsync("/api/Auth/verify-2fa", new { userId = user.Id, code = Code(secret) });
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("\"success\":false"));
        Assert.That(SetCookie(response, "auth_token"), Is.Null);
    }

    [Test]
    public async Task Login_Then2FA_WithChallenge_SignsIn()
    {
        var (_, secret) = await SeedTwoFactorUser("flow@test.local");
        using var client = Client();
        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email = "flow@test.local", password = "Passw0rd!" });
        var challenge = SetCookie(login, "2fa_challenge");
        Assert.That(challenge, Is.Not.Null);
        Assert.That(SetCookie(login, "auth_token"), Is.Null);

        using var verify = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/verify-2fa") { Content = JsonContent.Create(new { code = Code(secret) }) };
        verify.Headers.Add("Cookie", "2fa_challenge=" + challenge);
        var response = await client.SendAsync(verify);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("\"success\":true"));
        Assert.That(body, Does.Contain("\"token\":null"), "the JWT lives only in the HttpOnly cookie");
        Assert.That(SetCookie(response, "auth_token"), Is.Not.Null.And.Not.Empty);
    }

    // Brute force from one address must not lock everybody else out of login.
    [Test]
    public async Task LoginRateLimit_IsPerClientIp()
    {
        await using var factory = new Factory(); // fresh limiter state
        async Task<int> Login(IPAddress ip)
        {
            using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = ip)) { BaseAddress = new Uri("https://localhost") };
            return (int)(await client.PostAsJsonAsync("/api/Auth/login", new { email = "nobody@test.local", password = "x" })).StatusCode;
        }
        for (var i = 0; i < 5; i++) Assert.That(await Login(IPAddress.Loopback), Is.EqualTo(200));
        Assert.That(await Login(IPAddress.Loopback), Is.EqualTo(429));
        Assert.That(await Login(IPAddress.IPv6Loopback), Is.EqualTo(200));
    }

    // Still-valid JWTs stop working as soon as the account is banned or its role changes.
    [TestCase("ban")]
    [TestCase("role")]
    public async Task ChangedAccount_InvalidatesExistingToken(string change)
    {
        var id = change == "ban" ? 601 : 602;
        var token = Token(id, "Support");
        Assert.That(await Get("/api/Users/summary", token), Is.EqualTo(200));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FindAsync(id);
            if (change == "ban") user!.Status = "Banned"; else user!.Role = "Monitor";
            await db.SaveChangesAsync();
        }
        Assert.That(await Get("/api/Users/summary", token), Is.EqualTo(401));
    }

    // 2FA is enforced by the API, not only by the React route guard.
    [Test]
    public async Task WithoutTwoFactor_OnlySetupEndpointsWork()
    {
        var token = Token(603, "SuperAdmin");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Users.FindAsync(603))!.TwoFactorEnabled = false;
            await db.SaveChangesAsync();
        }
        Assert.That(await Get("/api/Auth/me", token), Is.EqualTo(200));
        Assert.That(await Get("/api/Users/summary", token), Is.EqualTo(401));
    }

    // Load-test switch: JMeter accounts without 2FA may call the API, but never in Production.
    [Test]
    public async Task AllowApiWithout2FA_LoadTestSwitch()
    {
        await using var loadTest = new Factory().WithWebHostBuilder(b => b.UseSetting("Security:AllowApiWithout2FA", "true"));
        using (var scope = loadTest.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = 607, Username = "jmeter", Email = "jmeter@test.local", Role = "SuperAdmin", TwoFactorEnabled = false });
            await db.SaveChangesAsync();
        }
        using var client = new HttpClient(loadTest.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback)) { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", Sign(607, "SuperAdmin", twoFactorEnabled: false));
        Assert.That((int)(await client.GetAsync("/api/Users/summary")).StatusCode, Is.EqualTo(200));

        await using var production = new Factory().WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("Security:AllowApiWithout2FA", "true"));
        var startup = Assert.Throws<InvalidOperationException>(() => _ = production.Server);
        Assert.That(startup!.Message, Does.Contain("AllowApiWithout2FA"));
    }

    [Test]
    public async Task Logout_RevokesTheToken()
    {
        _factory.EnsureSessionUser(604, "SuperAdmin");
        var token = Sign(604, "SuperAdmin", jti: Guid.NewGuid().ToString());
        Assert.That(await Get("/api/Auth/me", token), Is.EqualTo(200));

        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.That((int)(await client.PostAsync("/api/Auth/logout", null)).StatusCode, Is.EqualTo(200));

        Assert.That(await Get("/api/Auth/me", token), Is.EqualTo(401));
    }

    // Swagger stays available for the team, and every response carries the OWASP headers.
    [Test]
    public async Task SecurityHeaders_AreSent_AndSwaggerStillLoads()
    {
        using var client = Client();
        var response = await client.GetAsync("/api/index.html");
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Headers.GetValues("X-Content-Type-Options").Single(), Is.EqualTo("nosniff"));
        Assert.That(response.Headers.GetValues("X-Frame-Options").Single(), Is.EqualTo("SAMEORIGIN"));
        Assert.That(response.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("'unsafe-inline'"), "Swagger UI needs inline script");
        var spa = await client.GetAsync("/login");
        Assert.That(spa.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("script-src 'self' https://challenges.cloudflare.com;"), "the React app gets no inline/eval script (only the captcha script)");
        Assert.That((int)(await client.GetAsync("/swagger/v1/swagger.json")).StatusCode, Is.EqualTo(200));
    }

    // Editing a custom role's permissions (or unassigning it) ends sessions that still carry the old ones.
    [Test]
    public async Task ChangedPermissions_InvalidateExistingToken()
    {
        _factory.EnsureSessionUser(606, "QA Lead", "ManageUsers");
        var token = SignWithPermissions(606, "QA Lead", "ManageUsers");
        Assert.That(await Get("/api/Users/summary", token), Is.EqualTo(200));

        _factory.EnsureSessionUser(606, "QA Lead", "ManageOrders");
        Assert.That(await Get("/api/Users/summary", token), Is.EqualTo(401));
    }

    private static string SignWithPermissions(int userId, string role, params string[] permissions) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("EbayCloneTest", "EbayCloneTest",
            [new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
             new Claim(ClaimTypes.Role, role), .. permissions.Select(p => new Claim("permission", p))],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SensitiveLogHttpTests.SigningKey)), SecurityAlgorithms.HmacSha256)));

    // A token without jti could never be revoked, so it is not accepted at all.
    [Test]
    public async Task TokenWithoutJti_IsRejected()
    {
        _factory.EnsureSessionUser(605, "SuperAdmin");
        var noJti = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("EbayCloneTest", "EbayCloneTest",
            [new Claim(ClaimTypes.NameIdentifier, "605"), new Claim(ClaimTypes.Role, "SuperAdmin")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SensitiveLogHttpTests.SigningKey)), SecurityAlgorithms.HmacSha256)));
        Assert.That(await Get("/api/Auth/me", noJti), Is.EqualTo(401));
        Assert.That(await Get("/api/Auth/me", Sign(605, "SuperAdmin")), Is.EqualTo(200));
    }

    private static async Task<int> Get(string path, string token)
    {
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (int)(await client.GetAsync(path)).StatusCode;
    }

    // A TOTP code is single-use: replaying it (e.g. shoulder-surfed) inside its 30s window does not sign in again.
    [Test]
    public async Task SameTotpCode_CannotBeUsedTwice()
    {
        var (_, secret) = await SeedTwoFactorUser("replay@test.local");
        using var client = Client(IPAddress.IPv6Loopback); // own rate-limit partition
        var secondsLeft = 30 - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 30;
        if (secondsLeft < 5) await Task.Delay(TimeSpan.FromSeconds(secondsLeft)); // both sign-ins inside one TOTP window
        var code = Code(secret);
        async Task<string> SignIn()
        {
            var login = await client.PostAsJsonAsync("/api/Auth/login", new { email = "replay@test.local", password = "Passw0rd!" });
            using var verify = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/verify-2fa") { Content = JsonContent.Create(new { code }) };
            verify.Headers.Add("Cookie", "2fa_challenge=" + SetCookie(login, "2fa_challenge"));
            return await (await client.SendAsync(verify)).Content.ReadAsStringAsync();
        }
        Assert.That(await SignIn(), Does.Contain("\"success\":true"));
        Assert.That(await SignIn(), Does.Contain("\"success\":false").And.Contain("đã được sử dụng"));
    }

    private static async Task<(User User, string Secret)> SeedTwoFactorUser(string email, string role = "SuperAdmin")
    {
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var user = new User { Email = email, Username = email, Role = role, Status = "Active", ApprovalStatus = "Approved",
            Password = BCrypt.Net.BCrypt.HashPassword("Passw0rd!"), TwoFactorEnabled = true, TwoFactorSecret = secret };
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user, secret);
    }

    private static string Code(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    private static string? SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(v => v.StartsWith(name + "=")).Select(v => v[(name.Length + 1)..].Split(';')[0]).FirstOrDefault()
            : null;

    private static string Token(int userId, string role, bool twoFactorEnabled = true)
    {
        _factory.EnsureSessionUser(userId, role);
        return Sign(userId, role, twoFactorEnabled);
    }

    internal static string Sign(int userId, string role, bool twoFactorEnabled = true, string? jti = null) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("EbayCloneTest", "EbayCloneTest",
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role),
             new Claim("twoFactorEnabled", twoFactorEnabled ? "true" : "false"),
             new Claim(JwtRegisteredClaimNames.Jti, jti ?? Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SensitiveLogHttpTests.SigningKey)), SecurityAlgorithms.HmacSha256)));

    // TestServer has no remote IP by default; the IP middleware needs one
    private static HttpClient Client(IPAddress? remoteIp = null) =>
        new(_factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = remoteIp ?? IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
}
