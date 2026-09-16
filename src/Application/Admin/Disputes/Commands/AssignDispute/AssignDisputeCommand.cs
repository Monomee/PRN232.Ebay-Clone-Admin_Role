using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Application.Admin.Disputes.Commands.AssignDispute;

public record AssignDisputeCommand(int DisputeId, int? AdminId = null) : IRequest<bool>;

public class AssignDisputeCommandValidator : AbstractValidator<AssignDisputeCommand>
{
    public AssignDisputeCommandValidator()
    {
        RuleFor(v => v.DisputeId)
            .GreaterThan(0).WithMessage("Dispute ID is required.");
    }
}

public class AssignDisputeCommandHandler : IRequestHandler<AssignDisputeCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;
    private readonly ILogger<AssignDisputeCommandHandler> _logger;

    public AssignDisputeCommandHandler(
        IApplicationDbContext context,
        IUser currentUser,
        ILogger<AssignDisputeCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<bool> Handle(AssignDisputeCommand request, CancellationToken cancellationToken)
    {
        var dispute = await _context.Disputes
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

        Guard.Against.NotFound(request.DisputeId, dispute);

        // Get admin ID (use provided or current user)
        var adminId = request.AdminId;
        if (!adminId.HasValue && int.TryParse(_currentUser.Id, out var currentAdminId))
        {
            adminId = currentAdminId;
        }

        if (!adminId.HasValue)
        {
            throw new ValidationException("Admin ID could not be determined.");
        }

        var before = new
        {
            dispute.AssignedTo,
            dispute.AssignedAt,
            dispute.Status
        };

        // Update assignment
        dispute.AssignedTo = adminId.Value;
        dispute.AssignedAt = DateTime.UtcNow;

        // Update status if escalated
        if (dispute.Status == DisputeStatuses.Escalated)
        {
            dispute.Status = DisputeStatuses.AssignedToAdmin;
        }

        var after = new
        {
            dispute.AssignedTo,
            dispute.AssignedAt,
            dispute.Status
        };

        // Create system message
        var systemMessage = new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderId = adminId.Value,
            SenderType = SenderTypes.System,
            MessageType = MessageTypes.SystemUpdate,
            Content = $"Case assigned to admin for review",
            CreatedAt = DateTime.UtcNow,
            IsInternal = true
        };
        _context.DisputeMessages.Add(systemMessage);

        // Log admin action
        var adminAction = new AdminAction
        {
            AdminId = adminId ?? 0,
            Action = "AssignDispute",
            TargetType = "Dispute",
            TargetId = dispute.Id,
            Details = JsonSerializer.Serialize(new
            {
                caseId = dispute.CaseId,
                before,
                after
            }),
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(adminAction);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Dispute {CaseId} assigned to Admin {AdminId}",
            dispute.CaseId, adminId);

        return true;
    }
}
