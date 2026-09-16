using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Financials.Queries.GetPendingSettlementOrders;

public record PendingSettlementOrderDto
{
    public int Id { get; init; }
    public DateTime? OrderDate { get; init; }
    public decimal? TotalPrice { get; init; }
    public decimal? SellerEarnings { get; init; }
    public string? SellerName { get; init; }
    public string? BuyerName { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CanDisputeUntil { get; init; }
    public DateTime? EstimatedSettlementDate { get; init; }
}

public record GetPendingSettlementOrdersQuery : IRequest<List<PendingSettlementOrderDto>>;

public class GetPendingSettlementOrdersQueryHandler : IRequestHandler<GetPendingSettlementOrdersQuery, List<PendingSettlementOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingSettlementOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PendingSettlementOrderDto>> Handle(GetPendingSettlementOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _context.OrderTables
            .Where(o => o.Status == "Delivered" && o.CompletedAt != null && 
                        ((o.EstimatedSettlementDate != null && o.EstimatedSettlementDate <= DateTime.UtcNow) ||
                         (o.EstimatedSettlementDate == null && o.CanDisputeUntil < DateTime.UtcNow)))
            .Include(o => o.Buyer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.Seller)
            .ToListAsync(cancellationToken);

        return orders.Select(o => new PendingSettlementOrderDto
        {
            Id = o.Id,
            OrderDate = o.OrderDate,
            TotalPrice = o.TotalPrice,
            SellerEarnings = o.SellerEarnings,
            SellerName = o.OrderItems.FirstOrDefault()?.Product?.Seller?.Username ?? "Unknown Seller",
            BuyerName = o.Buyer?.Username ?? "Unknown Buyer",
            CompletedAt = o.CompletedAt,
            CanDisputeUntil = o.CanDisputeUntil,
            EstimatedSettlementDate = o.EstimatedSettlementDate
        }).ToList();
    }
}
