using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EbayClone.Application.Users.Commands.UpdateUserStatus;

public record UpdateUserStatusCommand : IRequest<bool>
{
    public int UserId { get; init; }
    public string Status { get; init; } = "Active"; // Active, Pending, Suspended
    public int AdminId { get; init; }
}

public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ISellerHubService _sellerHubService;
    private readonly ISender _sender;

    public UpdateUserStatusCommandHandler(IApplicationDbContext context, ISellerHubService sellerHubService, ISender sender)
    {
        _context = context;
        _sellerHubService = sellerHubService;
        _sender = sender;
    }

    public async Task<bool> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return false;
        }

        // Validate status
        var validStatuses = new[] { "Active", "Pending", "Suspended" };
        if (!validStatuses.Contains(request.Status))
        {
            throw new ArgumentException($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");
        }

        var before = new
        {
            user.Status,
            user.ApprovalStatus,
            user.BannedReason
        };

        user.Status = request.Status;

        var after = new
        {
            user.Status,
            user.ApprovalStatus,
            user.BannedReason
        };

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = request.AdminId,
            Action = "UpdateUserStatus",
            TargetType = "User",
            TargetId = user.Id,
            Details = JsonSerializer.Serialize(new
            {
                before,
                after
            }),
            CreatedAt = DateTime.UtcNow
        });

        if (request.Status is "Suspended" or "Banned")
        {
            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Canh bao trang thai tai khoan",
                Content = $"Trang thai tai khoan cua ban da duoc cap nhat thanh: {request.Status}.",
                Type = "InApp",
                Status = "Sent",
                SentAt = DateTime.UtcNow,
                CreatedBy = request.AdminId,
                CreatedAt = DateTime.UtcNow
            });
        }

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
