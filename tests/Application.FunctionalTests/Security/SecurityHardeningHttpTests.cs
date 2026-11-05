using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
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
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("\"success\":true"));
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
        Assert.That(response.Headers.Contains("Content-Security-Policy"));
        Assert.That((int)(await client.GetAsync("/swagger/v1/swagger.json")).StatusCode, Is.EqualTo(200));
    }

    private static async Task<int> Get(string path, string token)
    {
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (int)(await client.GetAsync(path)).StatusCode;
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

    private static string Sign(int userId, string role, bool twoFactorEnabled = true, string? jti = null) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("EbayCloneTest", "EbayCloneTest",
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role),
             new Claim("twoFactorEnabled", twoFactorEnabled ? "true" : "false"),
             .. jti == null ? Array.Empty<Claim>() : [new Claim(JwtRegisteredClaimNames.Jti, jti)]],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SensitiveLogHttpTests.SigningKey)), SecurityAlgorithms.HmacSha256)));

    // TestServer has no remote IP by default; the IP middleware needs one
    private static HttpClient Client(IPAddress? remoteIp = null) =>
        new(_factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = remoteIp ?? IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
}
