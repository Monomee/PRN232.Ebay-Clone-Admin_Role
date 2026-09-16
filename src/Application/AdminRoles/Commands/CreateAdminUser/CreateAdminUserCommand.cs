using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EbayClone.Application.AdminRoles.Commands.CreateAdminUser;

public record CreateAdminUserCommand : IRequest<int>
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int RoleId { get; init; }
    public int CreatedBy { get; init; }
}

public class CreateAdminUserCommandHandler : IRequestHandler<CreateAdminUserCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateAdminUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.AdminRoles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role == null)
        {
            throw new ArgumentException("Role not found.");
        }

        var duplicated = await _context.Users.AnyAsync(u =>
            (u.Username != null && u.Username == request.Username) ||
            (u.Email != null && u.Email == request.Email), cancellationToken);

        if (duplicated)
        {
            throw new ArgumentException("Username or email already exists.");
        }

        var adminUser = new User
        {
            Username = request.Username,
            Email = request.Email,
            Password = request.Password,
            Role = Roles.Administrator,
            Status = "Active",
            ApprovalStatus = "Approved",
            IsVerified = true
        };

        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync(cancellationToken);

        _context.AdminUserRoles.Add(new AdminUserRole
        {
            UserId = adminUser.Id,
            RoleId = request.RoleId,
            AssignedBy = request.CreatedBy,
            AssignedAt = DateTime.UtcNow
        });

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = request.CreatedBy,
            Action = "CreateAdminUser",
            TargetType = "User",
            TargetId = adminUser.Id,
            Details = JsonSerializer.Serialize(new
            {
                after = new
                {
                    adminUser.Username,
                    adminUser.Email,
                    roleId = request.RoleId,
                    roleName = role.RoleName
                }
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return adminUser.Id;
    }
}
