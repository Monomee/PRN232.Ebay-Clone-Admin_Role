using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EbayClone.Application.AdminRoles.Queries.GetAdminRoles;

public record GetAdminRolesQuery : IRequest<List<AdminRoleDto>>;

public class GetAdminRolesQueryHandler : IRequestHandler<GetAdminRolesQuery, List<AdminRoleDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminRolesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AdminRoleDto>> Handle(GetAdminRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _context.AdminRoles
            .Include(r => r.AdminUserRoles)
            .OrderBy(r => r.RoleName)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new AdminRoleDto
        {
            Id = r.Id,
            RoleName = r.RoleName,
            Description = r.Description,
            Permissions = ParsePermissions(r.Permissions),
            CreatedAt = r.CreatedAt,
            UserCount = r.AdminUserRoles?.Count ?? 0
        }).ToList();
    }

    private static List<string> ParsePermissions(string? permissions)
    {
        if (string.IsNullOrEmpty(permissions))
            return new List<string>();
        
        try
        {
            return JsonSerializer.Deserialize<List<string>>(permissions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
