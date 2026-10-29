using EbayClone.Domain.Constants;
using EbayClone.Infrastructure.Data.Seeders;
using EbayClone.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EbayClone.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var mode = ApplicationDbContextInitialiser.ValidateConfiguration(configuration, app.Environment);
        if (configuration.GetValue<bool>("DatabaseInitialization:RunMigrationsOnStartup"))
            await initialiser.InitialiseAsync();
        var protection = scope.ServiceProvider.GetRequiredService<EbayClone.Infrastructure.Services.SensitiveDataMigrationService>();
        if ((await protection.RunAsync(false)).NeedsConversion)
            throw new InvalidOperationException("Existing sensitive data needs an explicit maintenance-window conversion. Run --migrate-sensitive-data dry-run, then --apply with writers stopped.");
        await initialiser.SeedAsync(mode);
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IServiceProvider _serviceProvider;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _serviceProvider = serviceProvider;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
            _logger.LogInformation("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public static string ValidateConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var mode = configuration["Seeding:Mode"] ?? "None";
        if (mode is not ("None" or "System" or "Demo"))
            throw new InvalidOperationException("Seeding:Mode must be None, System or Demo.");
        _ = configuration.GetValue<bool>("DatabaseInitialization:RunMigrationsOnStartup");
        _ = configuration.GetValue<bool>("BackgroundJobs:Enabled");
        if (mode == "Demo" && !environment.IsDevelopment())
            throw new InvalidOperationException("Demo seeding is allowed only in Development.");
        return mode;
    }

    public async Task SeedAsync(string mode = "None")
    {
        // Validate even direct callers before opening a transaction.
        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var environment = _serviceProvider.GetRequiredService<IHostEnvironment>();
        if (mode != ValidateConfiguration(configuration, environment))
            throw new InvalidOperationException("Seed mode does not match validated configuration.");
        if (mode == "None") return;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (mode == "Demo" && await HasExistingDataAsync())
        {
            _logger.LogInformation("Demo seeding skipped: database already contains data.");
            return;
        }
        await SeedIdentityRolesAsync();
        if (mode == "System")
            await new AdminRolesSeeder(_context, _serviceProvider.GetRequiredService<ILogger<AdminRolesSeeder>>()).SeedAsync();
        else
        {
            await SeedDefaultAdminAsync();
            await ExecuteSeedersAsync();
        }
        await transaction.CommitAsync();
        _logger.LogInformation("Database seeding completed: {Mode}", mode);
    }

    private async Task<bool> HasExistingDataAsync()
    {
        if (await _context.Set<ApplicationUser>().AnyAsync()) return true;
        if (await _context.Addresses.AnyAsync()) return true;
        if (await _context.Bids.AnyAsync()) return true;
        if (await _context.Categories.AnyAsync()) return true;
        if (await _context.Coupons.AnyAsync()) return true;
        if (await _context.Disputes.AnyAsync()) return true;
        if (await _context.Feedbacks.AnyAsync()) return true;
        if (await _context.Inventories.AnyAsync()) return true;
        if (await _context.Messages.AnyAsync()) return true;
        if (await _context.OrderItems.AnyAsync()) return true;
        if (await _context.OrderTables.AnyAsync()) return true;
        if (await _context.Payments.AnyAsync()) return true;
        if (await _context.Products.AnyAsync()) return true;
        if (await _context.ReturnRequests.AnyAsync()) return true;
        if (await _context.Reviews.AnyAsync()) return true;
        if (await _context.ReviewReports.AnyAsync()) return true;
        if (await _context.ShippingInfos.AnyAsync()) return true;
        if (await _context.Stores.AnyAsync()) return true;
        if (await _context.Users.AnyAsync()) return true;
        if (await _context.ProductReports.AnyAsync()) return true;
        if (await _context.SellerWallets.AnyAsync()) return true;
        if (await _context.PlatformFees.AnyAsync()) return true;
        if (await _context.AdminActions.AnyAsync()) return true;
        if (await _context.Notifications.AnyAsync()) return true;
        if (await _context.UserNotificationReads.AnyAsync()) return true;
        if (await _context.AdminUserRoles.AnyAsync()) return true;
        if (await _context.FinancialTransactions.AnyAsync()) return true;
        if (await _context.DisputeMessages.AnyAsync()) return true;
        if (await _context.PayoutConfigs.AnyAsync()) return true;
        if (await _context.PayoutTransactions.AnyAsync()) return true;
        if (await _context.SellerLevelCriteria.AnyAsync()) return true;
        if (await _context.WithdrawalRequests.AnyAsync()) return true;
        if (await _context.SensitiveDataLogs.AnyAsync()) return true;
        return false;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Identity seeding failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }

    private async Task SeedIdentityRolesAsync()
    {
        var roleNames = new[]
        {
            Roles.Administrator,
            Roles.SuperAdmin,
            Roles.Monitor,
            Roles.Support
        };

        foreach (var roleName in roleNames)
        {
            if (_roleManager.Roles.All(r => r.Name != roleName))
            {
                EnsureSucceeded(await _roleManager.CreateAsync(new IdentityRole(roleName)));
                _logger.LogInformation("Seeded Identity Role: {Role}", roleName);
            }
        }
    }

    private async Task SeedDefaultAdminAsync()
    {
        var administrator = new ApplicationUser
        {
            UserName = "administrator@localhost",
            Email = "administrator@localhost",
            EmailConfirmed = true
        };

        if (_userManager.Users.All(u => u.UserName != administrator.UserName))
        {
            EnsureSucceeded(await _userManager.CreateAsync(administrator, "Administrator1!"));
            EnsureSucceeded(await _userManager.AddToRolesAsync(administrator, new[] { Roles.Administrator, Roles.SuperAdmin }));
            _logger.LogInformation("Seeded Default Admin User: {Email}", administrator.Email);
        }
    }

    private async Task ExecuteSeedersAsync()
    {
        // Get all seeder types
        var seederTypes = new List<Type>
        {
            typeof(AdminRolesSeeder),
            typeof(CategoriesSeeder),
            typeof(PlatformFeesSeeder),
            typeof(UsersSeeder),
            typeof(SellerWalletsSeeder),
            typeof(ProductsSeeder),
            typeof(StoresSeeder),
            typeof(AddressesSeeder),
            typeof(DisputesSeeder),
            typeof(ReturnRequestsSeeder),
            typeof(FinanceDemoSeeder),
            typeof(StatsDemoSeeder),
            typeof(AdditionalDataSeeder),
            typeof(PayoutEngineDemoSeeder),   // ← Payout Engine demo data
            typeof(SellerLevelCriteriaSeeder),
            typeof(SensitiveDataLogsSeeder)
        };

        // Create seeder instances and sort by Order property
        var seeders = seederTypes
            .Select(type =>
            {
                var logger = _serviceProvider.GetRequiredService(
                    typeof(ILogger<>).MakeGenericType(type));
                return (ISeeder)ActivatorUtilities.CreateInstance(_serviceProvider, type, _context, logger);
            })
            .OrderBy(s => s.Order)
            .ToList();

        // Execute seeders in order
        foreach (var seeder in seeders)
        {
            try
            {
                _logger.LogInformation("Executing seeder: {SeederName}", seeder.GetType().Name);
                await seeder.SeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing seeder: {SeederName}", seeder.GetType().Name);
                throw;
            }
        }
    }
}
