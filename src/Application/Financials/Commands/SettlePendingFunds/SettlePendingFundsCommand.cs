using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Financials.Commands.SettlePendingFunds;

public record SettlePendingFundsCommand : IRequest<int>;

public class SettlePendingFundsCommandHandler : IRequestHandler<SettlePendingFundsCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ISellerHubService _sellerHubService;

    public SettlePendingFundsCommandHandler(IApplicationDbContext context, ISellerHubService sellerHubService)
    {
        _context = context;
        _sellerHubService = sellerHubService;
    }

    public async Task<int> Handle(SettlePendingFundsCommand request, CancellationToken cancellationToken)
    {
        // 1. Find orders that are delivered and past the dispute window
        var eligibleOrders = await _context.OrderTables
            .Where(o => (o.Status == "Delivered" || o.Status == "PartiallyRefunded" || o.Status == "Refunded") && o.CompletedAt != null && 
                        ((o.EstimatedSettlementDate != null && o.EstimatedSettlementDate <= DateTime.UtcNow) ||
                         (o.EstimatedSettlementDate == null && o.CanDisputeUntil < DateTime.UtcNow)))
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .ToListAsync(cancellationToken);

        int settledCount = 0;
        var updatedWallets = new Dictionary<int, SellerWallet>();

        foreach (var order in eligibleOrders)
        {
            // Assuming one seller per order for simplicity, or we group by seller
            var sellerIds = order.OrderItems
                .Select(oi => oi.Product?.SellerId)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .Distinct();

            foreach (var sellerId in sellerIds)
            {
                var wallet = await _context.SellerWallets
                    .FirstOrDefaultAsync(w => w.SellerId == sellerId, cancellationToken);

                if (wallet != null)
                {
                    var amountToSettle = order.SellerEarnings ?? 0;
                    
                    if (amountToSettle > 0)
                    {
                        try
                        {
                            wallet.MovePendingToAvailable(amountToSettle);
                            
                            var transaction = new FinancialTransaction
                            {
                                SellerId = sellerId,
                                UserId = sellerId,
                                Type = "Settlement",
                                Amount = amountToSettle,
                                BalanceAfter = wallet.AvailableBalance,
                                OrderId = order.Id,
                                Description = $"Settled pending funds for order #{order.Id}",
                                Date = DateTime.UtcNow
                            };
                            _context.FinancialTransactions.Add(transaction);
                        }
                        catch (InvalidOperationException)
                        {
                            // Ignore the exception if PendingBalance was already depleted (e.g. disputes/refunds sync issues or demo data)
                            // We still want to mark the order as FundsCleared so it doesn't get picked up repeatedly 
                            // causing an infinite loop in the background service log.
                        }

                        order.Status = "FundsCleared"; 
                        settledCount++;
                        updatedWallets[sellerId] = wallet;
                    }
                }
            }
        }

        if (settledCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Broadcast real-time update for all settled sellers
            foreach (var (sellerId, wallet) in updatedWallets)
            {
                await _sellerHubService.BroadcastWalletUpdate(
                    sellerId, wallet.AvailableBalance, wallet.PendingBalance, wallet.TotalWithdrawn);
            }
        }

        return settledCount;
    }
}
