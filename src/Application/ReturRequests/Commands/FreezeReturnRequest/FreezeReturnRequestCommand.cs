using EbayClone.Application.Common.Exceptions;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.ReturRequests.Commands.FreezeReturnRequest;

public record FreezeReturnRequestCommand(int ReturnRequestId, string? AdminNote)
    : IRequest<Unit>;

public class FreezeReturnRequestCommandHandler
    : IRequestHandler<FreezeReturnRequestCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationNotifier _notifier;
    private readonly IUser _user;

    public FreezeReturnRequestCommandHandler(IApplicationDbContext context, INotificationNotifier notifier, IUser user)
    {
        _context = context;
        _notifier = notifier;
        _user = user;
    }

    public async Task<Unit> Handle(
        FreezeReturnRequestCommand request,
        CancellationToken cancellationToken)
    {
        var returnRequest = await _context.ReturnRequests
            .FirstOrDefaultAsync(r => r.Id == request.ReturnRequestId, cancellationToken);

        if (returnRequest == null)
            throw new NotFoundException(nameof(ReturnRequest), $"{request.ReturnRequestId}");

        // Only allow freezing if it hasn't been resolved yet
        if (returnRequest.Status == ReturnStatuses.Approved || 
            returnRequest.Status == ReturnStatuses.Rejected || 
            returnRequest.Status == ReturnStatuses.Completed)
        {
            throw new InvalidOperationException(
                $"Không thể đóng băng yêu cầu #{request.ReturnRequestId} vì đã được xử lý xong (Status: {returnRequest.Status}).");
        }

        returnRequest.Status = ReturnStatuses.Frozen;
        returnRequest.AdminNote = $"[FROZEN] {request.AdminNote}";
        if (int.TryParse(_user.Id, out int adminId))
        {
            returnRequest.ResolvedByAdminId = adminId;
        }
        
        await _context.SaveChangesAsync(cancellationToken);
        
        // Ghi log Admin Action
        var adminAction = new AdminAction
        {
            AdminId = int.Parse(_user.Id ?? "0"),
            Action = "FreezeReturnRequest",
            TargetType = "ReturnRequest",
            TargetId = returnRequest.Id,
            Details = $"Froze return request #{returnRequest.Id}. Reason: {request.AdminNote}",
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(adminAction);
        await _context.SaveChangesAsync(cancellationToken);
        
        // Broadcast update
        await _notifier.NotifyReturnRequestUpdatedAsync(returnRequest.Id, returnRequest.Status, cancellationToken);
        
        return Unit.Value;
    }
}
