using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

// Outside the SQL suite's SetUpFixture: HTTP authorization can run without Docker.
namespace EbayClone.Security.HttpTests;

public class SensitiveLogHttpTests
{
    internal const string SigningKey = "functional-test-signing-key-32bytes!";
    internal sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _database = Guid.NewGuid().ToString();

        /// Extra configuration for one test (e.g. opening InternalIps), applied after the defaults below.
        public Dictionary<string, string> Settings { get; init; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing")
                .UseSetting("ConnectionStrings:MyCnn", "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
                .UseSetting("Security:EncryptionKey", Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()))
                .UseSetting("Captcha:TurnstileSecretKey", "") // a real key in src/Web/appsettings.json would demand a real captcha
                .UseSetting("Jwt:Key", SigningKey).UseSetting("Jwt:Issuer", "EbayCloneTest").UseSetting("Jwt:Audience", "EbayCloneTest");
            foreach (var (key, value) in Settings) builder.UseSetting(key, value);
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                // src/Web/appsettings.json may hold real Gmail credentials: tests must never mail anyone.
                services.RemoveAll<IEmailService>().AddSingleton(new Mock<IEmailService>().Object);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_database));
                services.RemoveAll<IUser>();
                services.AddScoped<IUser, CurrentUser>();
            });
        }

        /// Every authenticated request is checked against the Users table (status, role, 2FA), so a test token needs
        /// a matching active account with 2FA on. Creates it, or brings an existing row in line with the token.
        public void EnsureSessionUser(int id, string role, params string[] permissions)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = db.Users.Find(id);
            if (user == null) db.Users.Add(user = new User { Id = id, Username = $"user{id}", Email = $"user{id}@test.local" });
            user.Role = role;
            user.Status = "Active";
            user.ApprovalStatus = "Approved";
            user.TwoFactorEnabled = true;
            db.SaveChanges();

            // Custom admin role permissions are checked against AdminUserRoles too
            var assignment = db.AdminUserRoles.Include(a => a.Role).FirstOrDefault(a => a.UserId == id);
            if (permissions.Length == 0)
            {
                if (assignment != null) db.AdminUserRoles.Remove(assignment);
            }
            else if (assignment == null)
                db.AdminUserRoles.Add(new AdminUserRole { UserId = id, Role = new AdminRole { RoleName = $"{role}#{id}", Permissions = JsonSerializer.Serialize(permissions) } });
            else
                assignment.Role!.Permissions = JsonSerializer.Serialize(permissions);
            db.SaveChanges();
        }
    }

    [TestCase(null, HttpStatusCode.Unauthorized, false)]
    [TestCase("Support", HttpStatusCode.Forbidden, false)]
    [TestCase("Monitor", HttpStatusCode.Forbidden, false)]
    [TestCase("Seller", HttpStatusCode.Forbidden, false)]
    [TestCase("SuperAdmin", HttpStatusCode.OK, false)]
    [TestCase("SuperAdmin", HttpStatusCode.BadRequest, true)]
    public async Task DecryptEndpoint_EnforcesRealJwtRoles_AndAuditsSuccessfulRead(string? role, HttpStatusCode expected, bool tamper)
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
            db.Users.Add(new User { Id = 42, Username = "superadmin", Role = role ?? "SuperAdmin", TwoFactorEnabled = true });
            db.SensitiveDataLogs.Add(new SensitiveDataLog { Id = 1, ActorId = 42, Action = "TestRead", TargetEntity = "User",
                EncryptedData = crypto.Encrypt("{\"cccd\":\"001234567890\"}"), DataHash = tamper ? new string('0', 64) : crypto.ComputeHmac("{\"cccd\":\"001234567890\"}") });
            await db.SaveChangesAsync();
        }
        string? token = role == null ? null : new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "EbayCloneTest", "EbayCloneTest", [new Claim("jti", Guid.NewGuid().ToString()), new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        // TestServer has no remote IP by default; use a trusted loopback connection through the real IP middleware.
        var response = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Scheme = "https";
            context.Request.Method = "POST";
            context.Request.Path = "/api/sensitive-logs/1/decrypt";
            if (token != null) context.Request.Headers.Authorization = "Bearer " + token;
        });
        Assert.That(response.Response.StatusCode, Is.EqualTo((int)expected));
        using var reader = new StreamReader(response.Response.Body);
        var body = await reader.ReadToEndAsync();
        if (expected == HttpStatusCode.OK)
        {
            Assert.That(body, Does.Contain("001234567890"));
            Assert.That(response.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        }
        else Assert.That(body, Does.Not.Contain("001234567890"));
        using var checkScope = factory.Services.CreateScope();
        var actions = await checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AdminActions.ToListAsync();
        Assert.That(actions.Count, Is.EqualTo(expected == HttpStatusCode.OK ? 1 : 0));
        if (expected == HttpStatusCode.OK) Assert.That(actions.Single().AdminId, Is.EqualTo(42));
    }
}
