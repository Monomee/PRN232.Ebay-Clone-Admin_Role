using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Stats.Queries;

public record OrderStatsDto
{
    public int Completed { get; init; }
    public int Delivered { get; init; }
    public int Returned  { get; init; }
    public int Total     { get; init; }
}

public record GetOrderStatsQuery(DateTime? StartDate, DateTime? EndDate) : IRequest<OrderStatsDto>;

public class GetOrderStatsQueryHandler : IRequestHandler<GetOrderStatsQuery, OrderStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetOrderStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderStatsDto> Handle(GetOrderStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.OrderTables.AsNoTracking();

        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.ToUniversalTime();
            query = query.Where(o => o.OrderDate >= start);
        }

        if (request.EndDate.HasValue)
        {
            var end = request.EndDate.Value.ToUniversalTime();
            query = query.Where(o => o.OrderDate <= end);
        }

        var orders = await query
            .Select(o => o.Status)
            .ToListAsync(cancellationToken);

        var completed = orders.Count(s => s == "Completed");
        var delivered = orders.Count(s => s == "Delivered");
        var returned  = orders.Count(s => s == "Returned"  || s == "ReturnApproved");

        return new OrderStatsDto
        {
            Completed = completed,
            Delivered = delivered,
            Returned  = returned,
            Total     = orders.Count
        };
    }
}
