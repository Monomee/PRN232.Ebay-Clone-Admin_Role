using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EbayClone.Web.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private const string AdminGroup = "Admins";

    public override async Task OnConnectedAsync()
    {
        // Join specific user group
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        // Join role-based groups
        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!string.IsNullOrEmpty(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, role);
        }

        if (role == "SuperAdmin" || role == "Support" || role == "Administrator" || role == "Monitor")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        // Remove from role-based groups
        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!string.IsNullOrEmpty(role))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, role);
        }

        if (role == "SuperAdmin" || role == "Support" || role == "Administrator" || role == "Monitor")
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
