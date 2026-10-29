using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Users.Commands.BanUser;

public record BanUserCommand : IRequest<bool>
{
    public int UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int AdminId { get; init; }
}

public class BanUserCommandHandler : IRequestHandler<BanUserCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ISensitiveDataLogger _sensitiveLog;

    private readonly ISellerHubService _sellerHubService;
    private readonly ISender _sender;

    public BanUserCommandHandler(IApplicationDbContext context, ISellerHubService sellerHubService, ISender sender, ISensitiveDataLogger sensitiveLog, IUser user)
    {
        _context = context;
        _user = user;
        _sensitiveLog = sensitiveLog;

        _sellerHubService = sellerHubService;
        _sender = sender;
    }

    public async Task<bool> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        var actorId = _user.GetRequiredActorId();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return false;
        }

        // Check if user has any active (non-terminal) transactions
        var terminalStatuses = new[] { "Completed", "Refunded", "FundsCleared", "Cancelled", "PartiallyRefunded" };

        var hasActiveOrders = await _context.OrderTables.AnyAsync(o =>
            (o.BuyerId == request.UserId ||
             o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == request.UserId))
            && (o.Status == null || !terminalStatuses.Contains(o.Status)),
            cancellationToken);

        if (hasActiveOrders)
        {
            throw new InvalidOperationException(
                "Cannot ban this user because they are currently involved in active transactions. " +
                "Please wait until all orders are completed, refunded, or cancelled before banning.");
        }

        var before = new
        {
            user.Status,
            user.ApprovalStatus,
            user.BannedReason,
            user.BannedBy,
            user.BannedAt
        };

        user.Status = "Banned";
        user.BannedReason = request.Reason;
        user.BannedBy = actorId;
        user.BannedAt = DateTime.UtcNow;

        var after = new
        {
            user.Status,
            user.ApprovalStatus,
            user.BannedReason,
            user.BannedBy,
            user.BannedAt
        };

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Account blocked",
            Content = $"Your account was blocked. Reason: {request.Reason}",
            Type = "InApp",
            Status = "Sent",
            SentAt = DateTime.UtcNow,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Canh bao tai khoan",
            Content = $"Tai khoan cua ban da bi khoa. Ly do: {request.Reason}",
            Type = "Email",
            Status = "Pending",
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = actorId,
            Action = "BanUser",
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

        _sensitiveLog.Add("BanUser", "User", user.Id.ToString(), "PII", new { before, after, request.Reason });
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
