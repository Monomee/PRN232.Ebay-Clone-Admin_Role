using EbayClone.Application.Common.Interfaces;
using EbayClone.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EbayClone.Web.Services;

/// <summary>
/// Implementation của IDisputeNotifier sử dụng SignalR.
/// Đặt ở Web layer vì cần tham chiếu đến DisputeHub (Hub).
/// 
/// Khi có Redis backplane, message được phân phối qua tất cả pod trong cluster
/// thông qua Redis Pub/Sub — không cần biết pod nào đang giữ connection của admin.
/// </summary>
public class DisputeNotifier : IDisputeNotifier
{
    private readonly IHubContext<DisputeHub> _hubContext;

    public DisputeNotifier(IHubContext<DisputeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyDisputeResolvedAsync(
        int disputeId,
        string caseId,
        string winner,
        decimal refundAmount,
        int resolvedBy,
        CancellationToken cancellationToken = default)
    {
        // Gửi event "DisputeResolved" đến tất cả admin đang kết nối
        // Redis backplane đảm bảo message được gửi đến admin ở BẤT KỲ pod nào
        await _hubContext.Clients
            .Group("Admins")
            .SendAsync(
                "DisputeResolved",
                new
                {
                    disputeId,
                    caseId,
                    winner,
                    refundAmount,
                    resolvedBy,
                    resolvedAt = DateTime.UtcNow
                },
                cancellationToken);
    }
}
