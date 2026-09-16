namespace EbayClone.Application.Common.Interfaces;

/// <summary>
/// Interface để gửi thông báo real-time khi dispute được resolve.
/// Được implement ở Infrastructure layer (SignalR), inject vào Application layer.
/// </summary>
public interface IDisputeNotifier
{
    /// <summary>
    /// Push sự kiện "dispute resolved" đến tất cả admin đang online qua SignalR.
    /// </summary>
    Task NotifyDisputeResolvedAsync(
        int disputeId,
        string caseId,
        string winner,
        decimal refundAmount,
        int resolvedBy,
        CancellationToken cancellationToken = default);
}
