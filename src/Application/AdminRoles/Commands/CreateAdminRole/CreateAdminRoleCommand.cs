using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using System.Text.Json;

namespace EbayClone.Application.AdminRoles.Commands.CreateAdminRole;

public record CreateAdminRoleCommand : IRequest<int>
{
    public string RoleName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> Permissions { get; init; } = new();
    public int CreatedBy { get; init; }
}

public class CreateAdminRoleCommandHandler : IRequestHandler<CreateAdminRoleCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _actor;

    public CreateAdminRoleCommandHandler(IApplicationDbContext context, IUser actor)
    {
        _context = context;
        _actor = actor;
    }

    public async Task<int> Handle(CreateAdminRoleCommand request, CancellationToken cancellationToken)
    {
        var actorId = _actor.GetRequiredActorId();
        var role = new AdminRole
        {
            RoleName = request.RoleName,
            Description = request.Description,
            Permissions = JsonSerializer.Serialize(request.Permissions),
            CreatedAt = DateTime.UtcNow
        };

        _context.AdminRoles.Add(role);

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = actorId,
            Action = "CreateAdminRole",
            TargetType = "AdminRole",
            Details = JsonSerializer.Serialize(new
            {
                after = new
                {
                    request.RoleName,
                    request.Description,
                    request.Permissions
                }
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}
