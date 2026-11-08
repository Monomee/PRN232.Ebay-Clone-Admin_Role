using EbayClone.Domain.Constants;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Common.Interfaces;

/// Permissions of the AdminRole assigned to a user (AdminRole.Permissions, a JSON array of policy names).
/// Issued as JWT claims so custom roles (e.g. "QA Lead") are authorized by what they grant, not by name.
public static class AdminPermissions
{
    public const string ClaimType = "permission";

    /// Policies a custom role can grant (AddGrantablePolicy in Infrastructure DI). Admin-role management,
    /// audit/sensitive logs and system pages check the SuperAdmin role itself and can never be granted.
    public static readonly string[] Grantable =
    [
        Policies.ViewDashboard, Policies.ViewReports, Policies.ManageUsers, Policies.ManageProducts,
        Policies.ManageOrders, Policies.ManageDisputes, Policies.ModerateReviews, Policies.ManageBroadcasts,
    ];

    public static async Task<List<string>> GetAdminPermissionsAsync(this IApplicationDbContext context, int userId, CancellationToken cancellationToken)
    {
        var json = await context.AdminUserRoles
            .Where(a => a.UserId == userId)
            .Select(a => a.Role!.Permissions)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
