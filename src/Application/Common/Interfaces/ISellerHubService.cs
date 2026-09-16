using EbayClone.Application.Sellers.Queries.GetSellerPerformanceMetrics;

namespace EbayClone.Application.Common.Interfaces;

public interface ISellerHubService
{
    Task BroadcastWalletUpdate(int sellerId, decimal availableBalance, decimal pendingBalance, decimal totalWithdrawn);
    Task BroadcastSellerMetricsUpdate(SellerPerformanceMetricsDto metrics);
}
