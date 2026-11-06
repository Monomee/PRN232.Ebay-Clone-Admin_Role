using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Constants;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Data.Interceptors;
using EbayClone.Infrastructure.Identity;
using EbayClone.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("MyCnn");
        Guard.Against.Null(connectionString, message: "Connection string 'MyCnn' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, SellerWalletChangedInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
        builder.Services.AddScoped<ISensitiveDataLogger, SensitiveDataLogger>();
        builder.Services.AddScoped<SensitiveDataMigrationService>();

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services
            .AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.SuperAdmin));

            // Business policies also accept a custom admin role that lists the policy in its Permissions.
            // Admin-role management, audit logs and financial endpoints stay system-role only.
            void AddGrantablePolicy(string name, params string[] roles) => options.AddPolicy(name, policy =>
                policy.RequireAssertion(ctx => roles.Any(ctx.User.IsInRole) || ctx.User.HasClaim(AdminPermissions.ClaimType, name)));

            AddGrantablePolicy(Policies.ViewDashboard, Roles.Monitor, Roles.Support, Roles.SuperAdmin);
            AddGrantablePolicy(Policies.ViewReports, Roles.Monitor, Roles.SuperAdmin);
            AddGrantablePolicy(Policies.ManageUsers, Roles.Support, Roles.SuperAdmin, Roles.Administrator);
            AddGrantablePolicy(Policies.ManageProducts, Roles.Support, Roles.SuperAdmin, Roles.Administrator);
            AddGrantablePolicy(Policies.ManageOrders, Roles.Support, Roles.SuperAdmin, Roles.Administrator);
            AddGrantablePolicy(Policies.ManageDisputes, Roles.Support, Roles.SuperAdmin, Roles.Administrator);
            AddGrantablePolicy(Policies.ModerateReviews, Roles.Support, Roles.SuperAdmin, Roles.Administrator);
            AddGrantablePolicy(Policies.ManageBroadcasts, Roles.Support, Roles.SuperAdmin, Roles.Administrator);

            options.AddPolicy(Policies.ManageAdminRoles, policy =>
                policy.RequireRole(Roles.SuperAdmin));

            options.AddPolicy(Policies.ViewAuditLogs, policy =>
                policy.RequireRole(Roles.SuperAdmin));
        });

        builder.Services.AddHttpClient<IContentModerationService, OpenAiModerationService>();
        builder.Services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>(c => c.Timeout = TimeSpan.FromSeconds(10));
        AccountLinking.Configure(builder.Configuration.GetSection("Risk").Get<RiskOptions>() ?? new RiskOptions());

        if (builder.Configuration.GetValue<bool>("BackgroundJobs:Enabled"))
        {
            builder.Services.AddHostedService<AutoReviewUsersService>();
            builder.Services.AddHostedService<ReturnRequestEscalationService>();
            builder.Services.AddHostedService<ScheduledBroadcastService>();
            builder.Services.AddHttpClient(nameof(DatacenterRangesService), c => c.Timeout = TimeSpan.FromSeconds(30));
            builder.Services.AddHostedService<DatacenterRangesService>();
        }

        // Payout Engine
        builder.Services.AddScoped<IMockPaymentGateway, MockPaymentGatewayService>();
    }
}
