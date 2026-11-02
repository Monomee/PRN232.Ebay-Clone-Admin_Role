using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Common.Interfaces;

/// Permissions of the AdminRole assigned to a user (AdminRole.Permissions, a JSON array of policy names).
/// Issued as JWT claims so custom roles (e.g. "QA Lead") are authorized by what they grant, not by name.
public static class AdminPermissions
{
    public const string ClaimType = "permission";

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
