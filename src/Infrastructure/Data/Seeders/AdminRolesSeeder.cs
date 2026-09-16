using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Data.Seeders;

public class AdminRolesSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminRolesSeeder> _logger;

    public int Order => 1;

    public AdminRolesSeeder(ApplicationDbContext context, ILogger<AdminRolesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var requiredRoles = new List<AdminRole>
        {
            new AdminRole
            {
                RoleName = "SuperAdmin",
                Description = "Full system access - Internal system administrator",
                Permissions = "[\"*\"]",
                CreatedAt = DateTime.UtcNow
            },
            new AdminRole
            {
                RoleName = "Monitor",
                Description = "Monitor role - View only for Dashboard and Reports/Stats",
                Permissions = "[\"ViewDashboard\",\"ViewReports\"]",
                CreatedAt = DateTime.UtcNow
            },
            new AdminRole
            {
                RoleName = "Support",
                Description = "Customer support - Manage Users, Products, Orders, Disputes, Reviews and Broadcast",
                Permissions = "[\"ViewDashboard\",\"ManageUsers\",\"ManageProducts\",\"ManageOrders\",\"ManageDisputes\",\"ModerateReviews\",\"ManageBroadcasts\"]",
                CreatedAt = DateTime.UtcNow
            }
        };

        var changed = false;
        foreach (var role in requiredRoles)
        {
            var existingRole = _context.AdminRoles.FirstOrDefault(r => r.RoleName == role.RoleName);
            if (existingRole == null)
            {
                _context.AdminRoles.Add(role);
                changed = true;
                continue;
            }

            if (existingRole.Description != role.Description || existingRole.Permissions != role.Permissions)
            {
                existingRole.Description = role.Description;
                existingRole.Permissions = role.Permissions;
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Ensured required admin roles are seeded: {Count}", requiredRoles.Count);
    }
}
