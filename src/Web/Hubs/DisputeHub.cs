using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EbayClone.Web.Hubs;

/// <summary>
/// SignalR Hub cho tính năng real-time của Dispute Management.
/// 
/// Khi chạy nhiều instance (load balancing), Redis backplane đảm bảo
/// message được broadcast đến đúng client dù nằm ở pod nào.
/// 
/// Hub path: /hubs/dispute
/// </summary>
[Authorize]
public class DisputeHub : Hub
{
    private const string AdminGroup = "Admins";

    /// <summary>
    /// Khi admin kết nối, tự động join vào group "Admins"
    /// để nhận tất cả notification liên quan đến dispute.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Khi disconnect, xoá khỏi group (SignalR tự làm nhưng explicit cho rõ)
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnDisconnectedAsync(exception);
    }
}
