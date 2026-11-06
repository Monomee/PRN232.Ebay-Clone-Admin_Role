using EbayClone.Infrastructure.Data;
using System;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Services;
using EbayClone.Web.Hubs;
using EbayClone.Web.Infrastructure; // <-- added for InternalIpMiddleware
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

if (args.Contains("--apply") && !args.Contains("--migrate-sensitive-data"))
    throw new ArgumentException("--apply requires --migrate-sensitive-data.");
var builder = WebApplication.CreateBuilder(args.Where(a => a is not "--migrate-sensitive-data" and not "--apply").ToArray());


// Add services to the container.
builder.AddKeyVaultIfConfigured();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false); // ẩn "Server: Kestrel"

// Ngắn hơn terminationGracePeriodSeconds (45) trừ preStop (15), để process tự thoát trước SIGKILL.
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(25);
});


var app = builder.Build();

// Resolve the durable key before any startup migration or seeder can write.
_ = app.Services.GetRequiredService<IEncryptionService>();

if (args.Contains("--migrate-sensitive-data"))
{
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<SensitiveDataMigrationService>()
        .RunAsync(args.Contains("--apply"));
    app.Logger.LogInformation("Sensitive-data conversion: Apply={Apply}, PiiFields={Fields}, Logs={Logs}, LookupHashes={Hashes}",
        args.Contains("--apply"), result.PiiFields, result.Logs, result.LookupHashes);
    return;
}

// Run DB migration + seeder on startup if not in testing environment (idempotent — safe to run every time)
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.InitialiseDatabaseAsync();
}

// Ensure real client IP is captured before any other middleware
app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>(); // CSP, nosniff, X-Frame-Options... cho mọi response (kể cả Swagger, 403)

// ── Environment-specific middleware ──────────────────────────
if (app.Environment.IsDevelopment())
{
    // Dev: nothing extra needed, seeder already ran above
}
else
{
    app.UseHsts();
}

// ── Core middleware (ORDER MATTERS) ──────────────────────────
// Liveness: process còn trả lời. Không mở SQL, để DB sập không làm kubelet giết pod.
app.UseHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: CanConnectAsync tới SQL Server. 503 thì pod ra khỏi Service.
app.UseHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.UseHealthChecks("/health");
app.UseHttpsRedirection();
app.UseMiddleware<InternalIpMiddleware>(); // <-- restrict to internal IPs early

app.UseStaticFiles();          // serve React build from wwwroot
app.UseExceptionHandler(options => { });
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseRateLimiter();          // Sau authentication để policy "authenticated" chia theo UserId
app.UseAuthorization();

// ── API endpoints ────────────────────────────────────────────
app.MapRazorPages();

// Register your endpoint groups

// Swagger lists every endpoint and lets anyone try them: off in Production unless Features:Swagger=true (e.g. a demo).
if (app.Configuration.GetValue<bool?>("Features:Swagger") ?? !app.Environment.IsProduction())
{
    app.UseOpenApi();
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/api";
    });
}

// ── SignalR Hubs ─────────────────────────────────────────────────
// ① MapHub phải đến TRƯỚC MapFallbackToFile
//   vì fallback bắt mọi route chưa match — kể cả /hubs/dispute/negotiate
// Redis backplane đảm bảo message được route đúng khi chạy 2+ pods.
app.MapHub<DisputeHub>("/hubs/dispute");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<SellerHub>("/hubs/seller");

app.MapEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
