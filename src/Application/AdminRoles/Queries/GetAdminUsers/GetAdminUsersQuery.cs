using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.AdminRoles.Queries.GetAdminUsers;

public record GetAdminUsersQuery : IRequest<PaginatedList<AdminUserRoleDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int? RoleId { get; init; }
    public string? Search { get; init; }
}

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, PaginatedList<AdminUserRoleDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AdminUserRoleDto>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var adminRoles = new[] { Roles.SuperAdmin, Roles.Support, Roles.Monitor, Roles.Administrator };

        var query = _context.Users
            .AsNoTracking()
            .Where(u => adminRoles.Contains(u.Role))
            .GroupJoin(_context.AdminUserRoles.Include(aur => aur.Role).Include(aur => aur.Assigner),
                u => u.Id,
                aur => aur.UserId,
                (u, aurs) => new { User = u, AdminUserRole = aurs.FirstOrDefault() })
            .AsQueryable();

        // Search by username or email
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(x =>
                (x.User.Username != null && x.User.Username.ToLower().Contains(searchLower)) ||
                (x.User.Email != null && x.User.Email.ToLower().Contains(searchLower)));
        }

        // Filter by role (AdminRole entity RoleId)
        if (request.RoleId.HasValue)
        {
            query = query.Where(x => x.AdminUserRole != null && x.AdminUserRole.RoleId == request.RoleId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.AdminUserRole != null ? x.AdminUserRole.AssignedAt : DateTime.MinValue)
            .OrderBy(x => x.User.Username)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new AdminUserRoleDto
            {
                Id = x.AdminUserRole != null ? x.AdminUserRole.Id : 0,
                UserId = x.User.Id,
                Username = x.User.Username,
                Email = x.User.Email,
                RoleId = x.AdminUserRole != null ? x.AdminUserRole.RoleId : 0,
                RoleName = x.AdminUserRole != null && x.AdminUserRole.Role != null ? x.AdminUserRole.Role.RoleName : (x.User.Role ?? ""),
                AssignedBy = x.AdminUserRole != null ? x.AdminUserRole.AssignedBy : null,
                AssignedByUsername = x.AdminUserRole != null && x.AdminUserRole.Assigner != null ? x.AdminUserRole.Assigner.Username : null,
                AssignedAt = x.AdminUserRole != null ? x.AdminUserRole.AssignedAt : null
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<AdminUserRoleDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
