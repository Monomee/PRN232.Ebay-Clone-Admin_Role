using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Orders.Queries.GetOrdersWithPagination;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Orders.Queries.GetOrderSummary;

/// Order Management cards over every order matching the list's filter (status, search). SuccessRate = completed / total, in percent.
public record OrderSummaryDto(int TotalOrders, decimal TotalRevenue, int ActiveOrders, int CompletedOrders, double SuccessRate);

public record GetOrderSummaryQuery(GetOrdersWithPaginationQuery Filter) : IRequest<OrderSummaryDto>;

public class GetOrderSummaryQueryHandler : IRequestHandler<GetOrderSummaryQuery, OrderSummaryDto>
{
    private static readonly string[] Cancelled = ["Cancelled", "Canceled", "Refunded"];
    private static readonly string[] Active = ["Pending", "Shipped"];
    private static readonly string[] Completed = ["Delivered", "Completed", "FundsCleared"];

    private readonly IApplicationDbContext _context;

    public GetOrderSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<OrderSummaryDto> Handle(GetOrderSummaryQuery request, CancellationToken cancellationToken)
    {
        var orders = GetOrdersWithPaginationQueryHandler.ApplyFilters(_context.OrderTables.AsNoTracking(), request.Filter);
        var total = await orders.CountAsync(cancellationToken);
        var completed = await orders.CountAsync(o => Completed.Contains(o.Status), cancellationToken);

        return new OrderSummaryDto(
            total,
            await orders.Where(o => !Cancelled.Contains(o.Status)).SumAsync(o => o.TotalPrice ?? 0, cancellationToken),
            await orders.CountAsync(o => Active.Contains(o.Status), cancellationToken),
            completed,
            total == 0 ? 0 : Math.Round(completed * 100.0 / total, 1));
    }
}
