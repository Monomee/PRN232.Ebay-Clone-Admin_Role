using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Application.Admin.Disputes.Commands.UpdateDisputePriority;

public record UpdateDisputePriorityCommand(int DisputeId, string Priority) : IRequest<bool>;

public class UpdateDisputePriorityCommandValidator : AbstractValidator<UpdateDisputePriorityCommand>
{
    public UpdateDisputePriorityCommandValidator()
    {
        RuleFor(v => v.DisputeId)
            .GreaterThan(0).WithMessage("Dispute ID is required.");

        RuleFor(v => v.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(p => p == DisputePriorities.Critical || 
                      p == DisputePriorities.High || 
                      p == DisputePriorities.Medium || 
                      p == DisputePriorities.Low)
            .WithMessage("Invalid priority value.");
    }
}

public class UpdateDisputePriorityCommandHandler : IRequestHandler<UpdateDisputePriorityCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;
    private readonly ILogger<UpdateDisputePriorityCommandHandler> _logger;

    public UpdateDisputePriorityCommandHandler(
        IApplicationDbContext context,
        IUser currentUser,
        ILogger<UpdateDisputePriorityCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateDisputePriorityCommand request, CancellationToken cancellationToken)
    {
        var dispute = await _context.Disputes
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

        Guard.Against.NotFound(request.DisputeId, dispute);

        var oldPriority = dispute.Priority;
        dispute.Priority = request.Priority;

        // Log admin action
        var adminId = int.TryParse(_currentUser.Id, out var parsedId) ? parsedId : (int?)null;
        var adminAction = new AdminAction
        {
            AdminId = adminId ?? 0,
            Action = "UpdateDisputePriority",
            TargetType = "Dispute",
            TargetId = dispute.Id,
            Details = JsonSerializer.Serialize(new
            {
                caseId = dispute.CaseId,
                before = new { priority = oldPriority },
                after  = new { priority = request.Priority }
            }),
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(adminAction);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Dispute {CaseId} priority changed from {OldPriority} to {NewPriority} by Admin {AdminId}",
            dispute.CaseId, oldPriority, request.Priority, adminId);

        return true;
    }
}
