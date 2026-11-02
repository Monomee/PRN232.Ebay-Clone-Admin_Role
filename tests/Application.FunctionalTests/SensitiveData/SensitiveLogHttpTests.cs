using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
    private const string SigningKey = "functional-test-signing-key-32bytes!";
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing")
                .UseSetting("ConnectionStrings:MyCnn", "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
                .UseSetting("Security:EncryptionKey", Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()))
                .UseSetting("Jwt:Key", SigningKey).UseSetting("Jwt:Issuer", "EbayCloneTest").UseSetting("Jwt:Audience", "EbayCloneTest");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_database));
                services.RemoveAll<IUser>();
                services.AddScoped<IUser, CurrentUser>();
            });
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
            db.Users.Add(new User { Id = 42, Username = "superadmin", Role = "SuperAdmin" });
            db.SensitiveDataLogs.Add(new SensitiveDataLog { Id = 1, ActorId = 42, Action = "TestRead", TargetEntity = "User",
                EncryptedData = crypto.Encrypt("{\"cccd\":\"001234567890\"}"), DataHash = tamper ? new string('0', 64) : crypto.ComputeHmac("{\"cccd\":\"001234567890\"}") });
            await db.SaveChangesAsync();
        }
        string? token = role == null ? null : new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "EbayCloneTest", "EbayCloneTest", [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, role)],
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
