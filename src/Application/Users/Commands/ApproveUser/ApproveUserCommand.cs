using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Users.Commands.ApproveUser;

public record ApproveUserCommand : IRequest<bool>
{
    public int UserId { get; init; }
    public int AdminId { get; init; }
}

public class ApproveUserCommandHandler : IRequestHandler<ApproveUserCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ISellerHubService _sellerHubService;
    private readonly ISender _sender;

    public ApproveUserCommandHandler(IApplicationDbContext context, ISellerHubService sellerHubService, ISender sender)
    {
        _context = context;
        _sellerHubService = sellerHubService;
        _sender = sender;
    }

    public async Task<bool> Handle(ApproveUserCommand request, CancellationToken cancellationToken)
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
            user.ApprovedBy,
            user.ApprovedAt
        };

        user.ApprovalStatus = "Approved";
        user.Status = "Active";
        user.ApprovedBy = request.AdminId;
        user.ApprovedAt = DateTime.UtcNow;

        var after = new
        {
            user.Status,
            user.ApprovalStatus,
            user.ApprovedBy,
            user.ApprovedAt
        };

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Account approved",
            Content = "Your account has been approved.",
            Type = "InApp",
            Status = "Sent",
            SentAt = DateTime.UtcNow,
            CreatedBy = request.AdminId,
            CreatedAt = DateTime.UtcNow
        });

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Thong bao phe duyet tai khoan",
            Content = "Tai khoan cua ban da duoc phe duyet.",
            Type = "Email",
            Status = "Pending",
            CreatedBy = request.AdminId,
            CreatedAt = DateTime.UtcNow
        });

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = request.AdminId,
            Action = "ApproveUser",
            TargetType = "User",
            TargetId = user.Id,
            Details = JsonSerializer.Serialize(new { before, after }),
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
