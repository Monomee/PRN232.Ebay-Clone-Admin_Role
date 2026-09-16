using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Commands.AccelerateSettlement;

public record AccelerateSettlementCommand : IRequest<int>
{
    public int SellerId { get; init; }
    public int SecondsFromNow { get; init; } = 60;
}

public class AccelerateSettlementCommandHandler : IRequestHandler<AccelerateSettlementCommand, int>
{
    private readonly IApplicationDbContext _context;

    public AccelerateSettlementCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(AccelerateSettlementCommand request, CancellationToken cancellationToken)
    {
        var targetTime = DateTime.UtcNow.AddSeconds(request.SecondsFromNow);
        
        var pendingOrders = await _context.OrderTables
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.Status == "Delivered" && o.CompletedAt != null)
            .Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == request.SellerId))
            .ToListAsync(cancellationToken);

        int updated = 0;
        foreach (var order in pendingOrders)
        {
            // Only update if it's currently scheduled further in the future
            if (order.EstimatedSettlementDate == null || order.EstimatedSettlementDate > targetTime)
            {
                order.EstimatedSettlementDate = targetTime;
                
                // Truncate milliseconds so the frontend has an accurate seconds representation
                order.EstimatedSettlementDate = new DateTime(
                    targetTime.Year, targetTime.Month, targetTime.Day, 
                    targetTime.Hour, targetTime.Minute, targetTime.Second, targetTime.Kind);
                    
                order.CanDisputeUntil = order.EstimatedSettlementDate; 
                updated++;
            }
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }
}
