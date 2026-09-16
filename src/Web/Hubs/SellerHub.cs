using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EbayClone.Web.Hubs;

[Authorize]
public class SellerHub : Hub
{
    private const string AdminGroup = "Admins";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnDisconnectedAsync(exception);
    }
}
