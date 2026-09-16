using EbayClone.Application.Common.Interfaces;
using EbayClone.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EbayClone.Web.Services;

public class NotificationNotifier : INotificationNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyNewNotificationAsync(
        int notificationId,
        string title,
        string content,
        string? userRole,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            id = notificationId,
            title,
            content,
            userRole,
            userId,
            createdAt = DateTime.UtcNow,
            isRead = false
        };

        if (userId.HasValue)
        {
            // Individual notification
            await _hubContext.Clients
                .Group($"User_{userId.Value}")
                .SendAsync("NewNotification", payload, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(userRole))
        {
            // Role-based notification
            // Note: If userRole is "All", we might want to use All group, 
            // but usually we just send to specific roles.
            // For "All", we can use the "Admins" group if it's for admins, 
            // or just broadcast to everyone.
            
            if (userRole.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                await _hubContext.Clients.All.SendAsync("NewNotification", payload, cancellationToken);
            }
            else
            {
                // Here we assume there are groups for specific roles like "Admins", "Seller", "Buyer"
                // The Hub's OnConnectedAsync should join users to these groups.
                // For now, let's just use the userRole as the group name if it's not "All"
                await _hubContext.Clients.Group(userRole).SendAsync("NewNotification", payload, cancellationToken);
            }
        }
        else
        {
            // General broadcast to all
            await _hubContext.Clients.All.SendAsync("NewNotification", payload, cancellationToken);
        }
    }

    public async Task NotifyReturnRequestUpdatedAsync(int requestId, string status, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ReturnRequestUpdated", new { requestId, status }, cancellationToken);
    }

    public async Task NotifyReturnRequestCreatedAsync(int requestId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ReturnRequestCreated", new { requestId }, cancellationToken);
    }

    public async Task NotifyProductBannedAsync(int productId, string reason, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("ProductBanned", new { productId, reason }, cancellationToken);
    }
}
