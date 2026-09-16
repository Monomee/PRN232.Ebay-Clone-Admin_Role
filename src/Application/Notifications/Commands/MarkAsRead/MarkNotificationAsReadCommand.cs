using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Notifications.Commands.MarkAsRead;

public record MarkNotificationAsReadCommand(int NotificationId) : IRequest<bool>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context, IUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var currentUserIdStr = _currentUser.Id;
        if (!int.TryParse(currentUserIdStr, out int userId))
        {
            return false;
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

        if (notification == null)
        {
            return false;
        }

        // If it's an individual notification for this user
        if (notification.UserId == userId)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        // If it's a broadcast notification (UserId is null)
        if (notification.UserId == null)
        {
            // Check if already marked as read
            var exists = await _context.UserNotificationReads
                .AnyAsync(r => r.NotificationId == request.NotificationId && r.UserId == userId, cancellationToken);

            if (!exists)
            {
                _context.UserNotificationReads.Add(new UserNotificationRead
                    {
                        NotificationId = request.NotificationId,
                        UserId = userId,
                        ReadAt = DateTime.UtcNow
                    });
                await _context.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        return false;
    }
}
