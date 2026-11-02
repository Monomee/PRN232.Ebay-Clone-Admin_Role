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

public record ProcessReturnLabelCommand(int ReturnRequestId, string ReturnLabelUrl, int? AdminId = null) : IRequest<Unit>;

public class ProcessReturnLabelCommandHandler : IRequestHandler<ProcessReturnLabelCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationNotifier _notifier;
    private readonly IUser? _user;

    public ProcessReturnLabelCommandHandler(IApplicationDbContext context, INotificationNotifier notifier, IUser? user = null)
    {
        _context = context;
        _notifier = notifier;
        _user = user;
    }

    public async Task<Unit> Handle(ProcessReturnLabelCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReturnLabelUrl))
            throw new ArgumentException("Please enter a return label URL.");

        var returnRequest = await _context.ReturnRequests
            .FirstOrDefaultAsync(r => r.Id == request.ReturnRequestId, cancellationToken);

        if (returnRequest == null)
            throw new NotFoundException(nameof(ReturnRequest), $"{request.ReturnRequestId}");

        if (returnRequest.Status != ReturnStatuses.WaitingForReturnLabel)
            throw new InvalidOperationException($"Yêu cầu này không ở trạng thái chờ mã vận đơn (Status: {returnRequest.Status}).");

        int adminId = request.AdminId.GetValueOrDefault();
        if (adminId <= 0 && !string.IsNullOrWhiteSpace(_user?.Id) && int.TryParse(_user.Id, out var parsedUserId))
        {
            adminId = parsedUserId;
        }
        if (adminId <= 0 && returnRequest.ResolvedByAdminId.HasValue && returnRequest.ResolvedByAdminId.Value > 0)
        {
            adminId = returnRequest.ResolvedByAdminId.Value;
        }
        if (adminId <= 0)
        {
            adminId = await _context.Users
                .Where(u => u.Role == Roles.SuperAdmin || u.Role == Roles.Support || u.Role == Roles.Administrator)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (adminId <= 0)
        {
            throw new UnauthorizedAccessException("AdminId is required to process return shipping label.");
        }

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
