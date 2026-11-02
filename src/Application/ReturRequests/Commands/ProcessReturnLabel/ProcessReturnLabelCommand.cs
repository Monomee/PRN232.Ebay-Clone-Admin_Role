using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.ReturRequests.Commands.ProcessReturnLabel;

public record ProcessReturnLabelCommand(int ReturnRequestId, string ReturnLabelUrl) : IRequest<Unit>;

public class ProcessReturnLabelCommandHandler : IRequestHandler<ProcessReturnLabelCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationNotifier _notifier;
    private readonly IUser _user;

    public ProcessReturnLabelCommandHandler(IApplicationDbContext context, INotificationNotifier notifier, IUser user)
    {
        _context = context;
        _notifier = notifier;
        _user = user;
    }

    public async Task<Unit> Handle(ProcessReturnLabelCommand request, CancellationToken cancellationToken)
    {
        var adminId = _user.GetRequiredActorId();

        if (string.IsNullOrWhiteSpace(request.ReturnLabelUrl))
            throw new ArgumentException("Please enter a return label URL.");

        var returnRequest = await _context.ReturnRequests
            .FirstOrDefaultAsync(r => r.Id == request.ReturnRequestId, cancellationToken);

        if (returnRequest == null)
            throw new NotFoundException(nameof(ReturnRequest), $"{request.ReturnRequestId}");

        if (returnRequest.Status != ReturnStatuses.WaitingForReturnLabel)
            throw new InvalidOperationException($"Yêu cầu này không ở trạng thái chờ mã vận đơn (Status: {returnRequest.Status}).");

        returnRequest.ReturnLabelUrl = request.ReturnLabelUrl.Trim();
        returnRequest.Status = ReturnStatuses.ReturnLabelProvided;
        returnRequest.ResolvedByAdminId = adminId;

        // Record Admin Action
        var adminAction = new AdminAction
        {
            AdminId = adminId,
            Action = "ProcessReturnLabel",
            TargetType = "ReturnRequest",
            TargetId = returnRequest.Id,
            Details = $"Provided return shipping label for request #{returnRequest.Id}. URL: {returnRequest.ReturnLabelUrl}",
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(adminAction);

        await _context.SaveChangesAsync(cancellationToken);

        // Broadcast SignalR notification
        await _notifier.NotifyReturnRequestUpdatedAsync(returnRequest.Id, returnRequest.Status, cancellationToken);

        return Unit.Value;
    }
}
