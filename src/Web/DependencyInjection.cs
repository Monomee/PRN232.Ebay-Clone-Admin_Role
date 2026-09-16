using System.Security.Claims;
using System.Text;
using Azure.Identity;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using EbayClone.Web.Hubs;
using EbayClone.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;


namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All;
            options.ForwardLimit = null; // Trust all proxies (loopback by default is trusted)
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
        });

        builder.Services.AddScoped<IUser, CurrentUser>();
        builder.Services.AddSingleton<IActiveConnectionTracker, ActiveConnectionTracker>(); // added for IP tracking

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>();

        builder.Services.AddHostedService<SellerEvaluationBackgroundService>();
        builder.Services.AddHostedService<SettlementBackgroundService>();
        builder.Services.AddHostedService<PayoutEngineBackgroundService>();

        builder.Services.AddExceptionHandler<CustomExceptionHandler>();

        builder.Services.AddRazorPages();

        // Rate Limiting — 3 policies: strict / standard / authenticated
        builder.Services.AddAppRateLimiting();

        // Customise default API behaviour
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IEmailService, EmailService>();

        // Configure NSwag to generate OpenAPI from Minimal API endpoints
        builder.Services.AddOpenApiDocument(configure =>
        {
            configure.Title = "EbayClone API";
            configure.Version = "v1";
            configure.AddSecurity("JWT", Enumerable.Empty<string>(), new NSwag.OpenApiSecurityScheme
            {
                Type = NSwag.OpenApiSecuritySchemeType.ApiKey,
                Name = "Authorization",
                In = NSwag.OpenApiSecurityApiKeyLocation.Header,
                Description = "Type into the textbox: Bearer {your JWT token}"
            });

            configure.OperationProcessors.Add(
                new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("JWT"));
        });

        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
            //đọc token từ cookie
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    // 1. Đọc token từ cookie (cho SPA thông thường)
                    ctx.Token = ctx.Request.Cookies["auth_token"];

                    // 2. Nếu không có cookie, đọc từ query string (cho SignalR WebSocket)
                    // Browsers không thể gửi Authorization header khi upgrade WS
                    if (string.IsNullOrEmpty(ctx.Token))
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            ctx.Token = accessToken;
                        }
                    }

                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("FrontendPolicy", policy =>
            {
                policy
                      // Dev: React chạy riêng ở localhost
                      .WithOrigins(
                          "http://localhost:3000",
                          "http://localhost:5000",
                          "http://localhost:5001",
                          "https://localhost:5001",
                          "https://localhost:44447"
                      )
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials(); // ✅ bắt buộc để cookie hoạt động
            });
        });

        // ── SignalR + Redis Backplane ───────────────────────────────────────────
        // DisputeHub + DisputeNotifier ở đây vì cần reference Hub (không thể ở Infrastructure)
        var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

        var signalRBuilder = builder.Services.AddSignalR();

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("ebay-dispute");
            });
        }

        builder.Services.AddScoped<ISellerHubService, SellerHubService>();
        builder.Services.AddScoped<IDisputeNotifier, DisputeNotifier>();
        builder.Services.AddScoped<INotificationNotifier, NotificationNotifier>();
    }

    public static void AddKeyVaultIfConfigured(this IHostApplicationBuilder builder)
    {
        var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
        }
    }
}
