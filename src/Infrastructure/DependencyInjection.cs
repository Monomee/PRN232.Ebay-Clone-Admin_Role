using EbayClone.Application.Common.Interfaces;
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

            options.AddPolicy(Policies.ViewDashboard, policy =>
                policy.RequireRole(Roles.Monitor, Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ViewReports, policy =>
                policy.RequireRole(Roles.Monitor, Roles.SuperAdmin));

            options.AddPolicy(Policies.ManageUsers, policy =>
                policy.RequireRole(Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ManageProducts, policy =>
                policy.RequireRole(Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ManageOrders, policy =>
                policy.RequireRole(Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ManageDisputes, policy =>
                policy.RequireRole(Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ModerateReviews, policy =>
                policy.RequireRole(Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ManageBroadcasts, policy =>
                policy.RequireRole(Roles.Support, Roles.SuperAdmin));

            options.AddPolicy(Policies.ManageAdminRoles, policy =>
                policy.RequireRole(Roles.SuperAdmin));

            options.AddPolicy(Policies.ViewAuditLogs, policy =>
                policy.RequireRole(Roles.SuperAdmin));
        });

        builder.Services.AddHttpClient<IContentModerationService, OpenAiModerationService>();

        builder.Services.AddHostedService<AutoReviewUsersService>();
        builder.Services.AddHostedService<ReturnRequestEscalationService>();

        // Payout Engine
        builder.Services.AddScoped<IMockPaymentGateway, MockPaymentGatewayService>();
    }
}
