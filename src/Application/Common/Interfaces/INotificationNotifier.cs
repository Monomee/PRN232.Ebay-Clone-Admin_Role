namespace EbayClone.Application.Common.Interfaces;

public interface INotificationNotifier
{
    Task NotifyNewNotificationAsync(int notificationId, string title, string content, string? userRole, int? userId, CancellationToken cancellationToken = default);
    Task NotifyReturnRequestUpdatedAsync(int requestId, string status, CancellationToken cancellationToken = default);
    Task NotifyReturnRequestCreatedAsync(int requestId, CancellationToken cancellationToken = default);
    Task NotifyProductBannedAsync(int productId, string reason, CancellationToken cancellationToken = default);
}
