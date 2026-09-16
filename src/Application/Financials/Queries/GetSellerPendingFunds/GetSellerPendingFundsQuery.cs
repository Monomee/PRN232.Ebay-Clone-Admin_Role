using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Financials.Queries.GetSellerPendingFunds;

public record SellerPendingFundDto
{
    public int OrderId { get; init; }
    public DateTime? OrderDate { get; init; }
    public decimal SellerEarnings { get; init; }
    public string? Status { get; init; }
    public DateTime? EstimatedSettlementDate { get; init; }
    public double DaysRemaining { get; init; }
}

public record GetSellerPendingFundsQuery(int SellerId) : IRequest<List<SellerPendingFundDto>>;

public class GetSellerPendingFundsQueryHandler : IRequestHandler<GetSellerPendingFundsQuery, List<SellerPendingFundDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSellerPendingFundsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SellerPendingFundDto>> Handle(GetSellerPendingFundsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Tìm các đơn hàng đã Delivered hoặc PartiallyRefunded/Refunded (sau Dispute) nhưng chưa quyết toán (Status != FundsCleared)
        // Và đơn hàng đó phải có sản phẩm của Seller này
        var orders = await _context.OrderTables
            .Where(o => (o.Status == "Delivered" || o.Status == "PartiallyRefunded" || o.Status == "Refunded") && 
                        o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == request.SellerId))
            .OrderBy(o => o.EstimatedSettlementDate)
            .ToListAsync(cancellationToken);

        return orders.Select(o => {
            DateTime? targetDate = o.EstimatedSettlementDate ?? o.CanDisputeUntil;
            double daysRemaining = 0;
            
            if (targetDate.HasValue)
            {
                daysRemaining = Math.Max(0, (targetDate.Value - now).TotalDays);
            }

            return new SellerPendingFundDto
            {
                OrderId = o.Id,
                OrderDate = o.OrderDate,
                SellerEarnings = o.SellerEarnings ?? 0,
                Status = o.Status,
                EstimatedSettlementDate = targetDate,
                DaysRemaining = Math.Round(daysRemaining, 1)
            };
        }).ToList();
    }
}
