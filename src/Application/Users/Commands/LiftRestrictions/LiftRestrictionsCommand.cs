using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users.Commands.LiftRestrictions;

/// Admin undo for the automatic risk measures (listing pause, payout hold). The stored risk score stays,
/// so the monitor only acts again on stronger evidence than what the admin already looked at.
public record LiftRestrictionsCommand(int UserId) : IRequest<bool>;

public class LiftRestrictionsCommandHandler(IApplicationDbContext context, IUser actor, ISensitiveDataLogger sensitiveLog)
    : IRequestHandler<LiftRestrictionsCommand, bool>
{
    public async Task<bool> Handle(LiftRestrictionsCommand request, CancellationToken cancellationToken)
    {
        var actorId = actor.GetRequiredActorId();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null) return false;

        var before = new { user.ProductBanUntil, user.PayoutHoldUntil };
        user.ProductBanUntil = null;
        user.PayoutHoldUntil = null;

        context.AdminActions.Add(new AdminAction
        {
            AdminId = actorId, Action = "LiftRestrictions", TargetType = "User", TargetId = user.Id,
            Details = JsonSerializer.Serialize(new { before, user.RiskScore }), CreatedAt = DateTime.UtcNow,
        });
        sensitiveLog.Add("LiftRestrictions", "User", user.Id.ToString(), "PII", new { before, user.RiskScore });
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
