using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EbayClone.Application.AdminRoles.Commands.AssignRole;

public record AssignRoleCommand : IRequest<bool>
{
    public int UserId { get; init; }
    public int RoleId { get; init; }
    public int AssignedBy { get; init; }
}

public class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AssignRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        // Check if user exists
        var user = await _context.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null)
            return false;

        // Check if role exists
        var role = await _context.AdminRoles.FindAsync(new object[] { request.RoleId }, cancellationToken);
        if (role == null)
            return false;

        // Check if assignment already exists
        var existingAssignment = await _context.AdminUserRoles
            .FirstOrDefaultAsync(aur => aur.UserId == request.UserId, cancellationToken);

        var before = existingAssignment == null
            ? null
            : new
            {
                existingAssignment.RoleId,
                existingAssignment.AssignedBy,
                existingAssignment.AssignedAt
            };

        if (existingAssignment != null)
        {
            // Update existing assignment
            existingAssignment.RoleId = request.RoleId;
            existingAssignment.AssignedBy = request.AssignedBy;
            existingAssignment.AssignedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new assignment
            var assignment = new AdminUserRole
            {
                UserId = request.UserId,
                RoleId = request.RoleId,
                AssignedBy = request.AssignedBy,
                AssignedAt = DateTime.UtcNow
            };
            _context.AdminUserRoles.Add(assignment);
        }

        // Keep User.Role in sync
        user.Role = role.RoleName;

        _context.Notifications.Add(new Notification
        {
            UserId = request.UserId,
            Title = "Phan quyen admin da duoc cap nhat",
            Content = $"Ban da duoc gan vai tro admin: {role.RoleName}.",
            Type = "InApp",
            Status = "Sent",
            SentAt = DateTime.UtcNow,
            CreatedBy = request.AssignedBy,
            CreatedAt = DateTime.UtcNow
        });

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = request.AssignedBy,
            Action = "AssignAdminRole",
            TargetType = "User",
            TargetId = request.UserId,
            Details = JsonSerializer.Serialize(new
            {
                before,
                after = new
                {
                    roleId = request.RoleId,
                    roleName = role.RoleName,
                    assignedBy = request.AssignedBy
                }
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
