using System;
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

    public ProcessReturnLabelCommandHandler(IApplicationDbContext context, INotificationNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    public async Task<Unit> Handle(ProcessReturnLabelCommand request, CancellationToken cancellationToken)
    {
        var returnRequest = await _context.ReturnRequests
            .FirstOrDefaultAsync(r => r.Id == request.ReturnRequestId, cancellationToken);

        if (returnRequest == null)
            throw new NotFoundException(nameof(ReturnRequest), $"{request.ReturnRequestId}");

        if (returnRequest.Status != ReturnStatuses.WaitingForReturnLabel)
            throw new InvalidOperationException($"Yêu cầu này không ở trạng thái chờ mã vận đơn (Status: {returnRequest.Status}).");

        returnRequest.ReturnLabelUrl = request.ReturnLabelUrl;
        returnRequest.Status = ReturnStatuses.ReturnLabelProvided;

        // Record Admin Action
        var adminAction = new AdminAction
        {
            Action = "ProcessReturnLabel",
            TargetType = "ReturnRequest",
            TargetId = returnRequest.Id,
            Details = $"Provided return shipping label for request #{returnRequest.Id}. URL: {request.ReturnLabelUrl}",
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(adminAction);

        await _context.SaveChangesAsync(cancellationToken);

        // Broadcast SignalR notification
        await _notifier.NotifyReturnRequestUpdatedAsync(returnRequest.Id, returnRequest.Status, cancellationToken);

        return Unit.Value;
    }
}
