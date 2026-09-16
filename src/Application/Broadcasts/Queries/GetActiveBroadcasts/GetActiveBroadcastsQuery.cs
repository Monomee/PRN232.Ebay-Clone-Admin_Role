using EbayClone.Application.Broadcasts.Queries.GetBroadcasts;
using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EbayClone.Application.Broadcasts.Queries.GetActiveBroadcasts;

public record GetActiveBroadcastsQuery(List<string> UserRoles) : IRequest<List<BroadcastDto>>;

public class GetActiveBroadcastsQueryHandler : IRequestHandler<GetActiveBroadcastsQuery, List<BroadcastDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;

    public GetActiveBroadcastsQueryHandler(IApplicationDbContext context, IUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<BroadcastDto>> Handle(GetActiveBroadcastsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentUserIdStr = _currentUser.Id;
        int? currentUserId = null;
        if (int.TryParse(currentUserIdStr, out int id))
        {
            currentUserId = id;
        }

        var query = _context.Notifications
            .Include(n => n.Creator)
            .Where(n => n.UserId == null || n.UserId == currentUserId) // Broadcasts OR for this user
            .Where(n => n.Status == "Sent" || (n.Status == "Scheduled" && n.ScheduledAt <= now))
            .AsQueryable();

        // If UserRoles is provided, return broadcasts for All or specifically for their roles
        if (request.UserRoles != null && request.UserRoles.Any())
        {
            query = query.Where(n => n.UserId == currentUserId || string.IsNullOrEmpty(n.UserRole) || request.UserRoles.Contains(n.UserRole));
        }
        else
        {
            // If they have no role (or are logged out somehow but hitting this), only show "All"
            query = query.Where(n => n.UserId == currentUserId || string.IsNullOrEmpty(n.UserRole));
        }

        var broadcasts = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50) // Limit to 50 for performance
            .ToListAsync(cancellationToken);

        // Get read broadcast IDs for this user
        var readBroadcastIds = new HashSet<int>();
        if (currentUserId.HasValue)
        {
            readBroadcastIds = (await _context.UserNotificationReads
                .Where(r => r.UserId == currentUserId.Value)
                .Select(r => r.NotificationId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        return broadcasts.Select(n => new BroadcastDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            UserRole = n.UserRole,
            Type = n.Type,
            Status = n.Status,
            ScheduledAt = n.ScheduledAt,
            SentAt = n.SentAt,
            CreatedBy = n.CreatedBy,
            CreatedByUsername = n.Creator != null ? n.Creator.Username : null,
            CreatedAt = n.CreatedAt,
            IsRead = n.UserId == currentUserId ? n.IsRead : readBroadcastIds.Contains(n.Id)
        }).ToList();
    }
}
