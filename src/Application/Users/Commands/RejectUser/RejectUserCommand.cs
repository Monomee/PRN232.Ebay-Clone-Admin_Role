using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EbayClone.Application.Users.Commands.RejectUser;

public record RejectUserCommand : IRequest<bool>
{
    public int UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int AdminId { get; init; }
}

public class RejectUserCommandHandler : IRequestHandler<RejectUserCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ISellerHubService _sellerHubService;
    private readonly ISender _sender;

    public RejectUserCommandHandler(IApplicationDbContext context, ISellerHubService sellerHubService, ISender sender)
    {
        _context = context;
        _sellerHubService = sellerHubService;
        _sender = sender;
    }

    public async Task<bool> Handle(RejectUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return false;
        }

        var before = new
        {
            user.Status,
            user.ApprovalStatus,
            user.BannedReason
        };

        user.ApprovalStatus = "Rejected";
        user.BannedReason = request.Reason; // Store rejection reason

        var after = new
        {
            user.Status,
            user.ApprovalStatus,
            user.BannedReason
        };

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Tai khoan bi tu choi phe duyet",
            Content = $"Yeu cau dang ky cua ban bi tu choi. Ly do: {request.Reason}",
            Type = "InApp",
            Status = "Sent",
            SentAt = DateTime.UtcNow,
            CreatedBy = request.AdminId,
            CreatedAt = DateTime.UtcNow
        });

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Thong bao tu choi phe duyet",
            Content = $"Yeu cau dang ky cua ban bi tu choi. Ly do: {request.Reason}",
            Type = "Email",
            Status = "Pending",
            CreatedBy = request.AdminId,
            CreatedAt = DateTime.UtcNow
        });

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = request.AdminId,
            Action = "RejectUser",
            TargetType = "User",
            TargetId = user.Id,
            Details = JsonSerializer.Serialize(new
            {
                reason = request.Reason,
                before,
                after
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        if (user.Role == "Seller" || await _context.Stores.AnyAsync(s => s.SellerId == user.Id, cancellationToken))
        {
            var metricsList = await _sender.Send(new EbayClone.Application.Sellers.Queries.GetSellerPerformanceMetrics.GetSellerPerformanceMetricsQuery(), cancellationToken);
            var metric = metricsList.FirstOrDefault(m => m.Id == user.Id);
            if (metric != null)
            {
                await _sellerHubService.BroadcastSellerMetricsUpdate(metric);
            }
        }

        return true;
    }
}
