using EbayClone.Domain.Constants;
using EbayClone.Infrastructure.Data.Seeders;
using EbayClone.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
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

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        _logger.LogInformation("Starting database seeding...");

        // 1. Seed Identity Roles (Required for admin user)
        await SeedIdentityRolesAsync();

        // 2. Seed Default Admin User (Required first)
        await SeedDefaultAdminAsync();

        // 3. Execute all custom seeders in order
        await ExecuteSeedersAsync();

        _logger.LogInformation("Database seeding completed successfully");
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
                await _roleManager.CreateAsync(new IdentityRole(roleName));
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
            await _userManager.CreateAsync(administrator, "Administrator1!");
            await _userManager.AddToRolesAsync(administrator, new[] { Roles.Administrator, Roles.SuperAdmin });
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
            typeof(SellerLevelCriteriaSeeder)
        };

        // Create seeder instances and sort by Order property
        var seeders = seederTypes
            .Select(type =>
            {
                var logger = _serviceProvider.GetRequiredService(
                    typeof(ILogger<>).MakeGenericType(type));
                return (ISeeder)Activator.CreateInstance(type, _context, logger)!;
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
