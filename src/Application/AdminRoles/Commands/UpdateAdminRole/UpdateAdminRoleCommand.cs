using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using System.Text.Json;

namespace EbayClone.Application.AdminRoles.Commands.UpdateAdminRole;

/// Changes what a custom role grants. Admins holding it lose or gain access on their next request
/// (SessionValidation compares the token's permission claims with the role).
public record UpdateAdminRoleCommand(int Id, string? Description, List<string> Permissions) : IRequest<bool>;

public class UpdateAdminRoleCommandValidator : AbstractValidator<UpdateAdminRoleCommand>
{
    public UpdateAdminRoleCommandValidator()
    {
        RuleFor(v => v.Permissions).NotNull();
        RuleForEach(v => v.Permissions).Must(p => AdminPermissions.Grantable.Contains(p))
            .WithMessage(p => $"Unknown permission. Allowed: {string.Join(", ", AdminPermissions.Grantable)}.");
    }
}

public class UpdateAdminRoleCommandHandler(IApplicationDbContext context, ISensitiveDataLogger sensitiveLog, IUser actor)
    : IRequestHandler<UpdateAdminRoleCommand, bool>
{
    public async Task<bool> Handle(UpdateAdminRoleCommand request, CancellationToken cancellationToken)
    {
        var actorId = actor.GetRequiredActorId();
        var role = await context.AdminRoles.FindAsync([request.Id], cancellationToken);
        if (role == null) return false;

        var before = new { role.Description, role.Permissions };
        role.Description = request.Description;
        role.Permissions = JsonSerializer.Serialize(request.Permissions.Distinct().ToList());

        context.AdminActions.Add(new AdminAction
        {
            AdminId = actorId,
            Action = "UpdateAdminRole",
            TargetType = "AdminRole",
            TargetId = role.Id,
            Details = JsonSerializer.Serialize(new { before, after = new { role.Description, role.Permissions } }),
            CreatedAt = DateTime.UtcNow
        });
        sensitiveLog.Add("UpdateAdminRole", "AdminRole", role.Id.ToString(), "AdminPrivilege",
            new { role.RoleName, before = before.Permissions, after = role.Permissions });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
