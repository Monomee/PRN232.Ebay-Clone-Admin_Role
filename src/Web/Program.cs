using EbayClone.Infrastructure.Data;
using System;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Services;
using EbayClone.Web.Hubs;
using EbayClone.Web.Infrastructure; // <-- added for InternalIpMiddleware
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.AddKeyVaultIfConfigured();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();


var app = builder.Build();

// Run DB migration + seeder on startup (idempotent — safe to run every time)
await app.InitialiseDatabaseAsync();

// Ensure real client IP is captured before any other middleware
app.UseForwardedHeaders();

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
app.UseHealthChecks("/health");
app.UseHttpsRedirection();
app.UseMiddleware<InternalIpMiddleware>(); // <-- restrict to internal IPs early

app.UseStaticFiles();          // serve React build from wwwroot
app.UseExceptionHandler(options => { });
app.UseCors("FrontendPolicy");
app.UseRateLimiter();          // Rate Limiting — TRƯỚC authentication
app.UseAuthentication();
app.UseAuthorization();

// ── API endpoints ────────────────────────────────────────────
app.MapRazorPages();

// Register your endpoint groups

app.UseOpenApi();

app.UseSwaggerUi(settings =>
{
    settings.Path = "/api";
});

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
