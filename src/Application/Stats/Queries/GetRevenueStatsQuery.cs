using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Stats.Queries;

public record DailyRevenueStatDto(DateTime Date, decimal Amount);

public record RevenueStatsDto
{
    public decimal TotalRevenue { get; init; }
    public int TotalTransactions { get; init; }
    public List<DailyRevenueStatDto> DailyRevenue { get; init; } = new();
}

public record GetRevenueStatsQuery(DateTime? StartDate, DateTime? EndDate) : IRequest<RevenueStatsDto>;

public class GetRevenueStatsQueryHandler : IRequestHandler<GetRevenueStatsQuery, RevenueStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetRevenueStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RevenueStatsDto> Handle(GetRevenueStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.Type == "FeeDeduction");

        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.ToUniversalTime();
            query = query.Where(t => t.Date >= start);
        }

        if (request.EndDate.HasValue)
        {
            var end = request.EndDate.Value.ToUniversalTime();
            query = query.Where(t => t.Date <= end);
        }

        // Fetch raw rows – two columns only, then group client-side.
        // GroupBy(t => t.Date.Date) cannot be translated to SQL by EF Core.
        var rawRows = await query
            .Select(t => new { t.Date, t.Amount })
            .ToListAsync(cancellationToken);

        var totalRevenue      = rawRows.Sum(t => t.Amount);
        var totalTransactions = rawRows.Count;

        var dailyRevenue = rawRows
            .GroupBy(t => t.Date.Date)
            .Select(g => new DailyRevenueStatDto(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(x => x.Date)
            .ToList();

        return new RevenueStatsDto
        {
            TotalRevenue      = totalRevenue,
            TotalTransactions = totalTransactions,
            DailyRevenue      = dailyRevenue
        };
    }
}
