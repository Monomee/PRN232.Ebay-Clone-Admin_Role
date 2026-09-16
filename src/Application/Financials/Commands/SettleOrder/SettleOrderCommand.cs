using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Financials.Commands.SettleOrder;

public record SettleOrderCommand(int OrderId) : IRequest<bool>;

public class SettleOrderCommandHandler : IRequestHandler<SettleOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public SettleOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(SettleOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.OrderTables
            .Where(o => o.Id == request.OrderId && o.Status == "Delivered")
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null) return false;

        // Verify eligibility (past dispute window)
        if (order.CanDisputeUntil > DateTime.UtcNow)
        {
            // It's still in the dispute window. In a real app we might return an error message,
            // but for simplicity we return false.
            return false;
        }

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
                    wallet.MovePendingToAvailable(amountToSettle);
                    
                    // Add a transaction record
                    var transaction = new FinancialTransaction
                    {
                        SellerId = sellerId,
                        UserId = sellerId,
                        Type = "Settlement",
                        Amount = amountToSettle,
                        BalanceAfter = wallet.AvailableBalance,
                        OrderId = order.Id,
                        Description = $"Settlement for order #{order.Id}",
                        Date = DateTime.UtcNow
                    };
                    _context.FinancialTransactions.Add(transaction);
                }
            }
        }

        order.Status = "FundsCleared";
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
