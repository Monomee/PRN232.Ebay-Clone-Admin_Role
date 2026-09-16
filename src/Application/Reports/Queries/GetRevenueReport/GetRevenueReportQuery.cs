using EbayClone.Application.Common.Interfaces;
using MediatR;

namespace EbayClone.Application.Reports.Queries.GetRevenueReport;

public record RevenueReportDto
{
    public decimal TotalRevenue { get; init; }
    public decimal RevenueThisMonth { get; init; }
    public int TotalTransactions { get; init; }
    public List<DailyRevenueDto> DailyRevenue { get; init; } = new();
}

public record DailyRevenueDto(DateTime Date, decimal Amount);

public record GetRevenueReportQuery : IRequest<RevenueReportDto>;

public class GetRevenueReportQueryHandler : IRequestHandler<GetRevenueReportQuery, RevenueReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetRevenueReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RevenueReportDto> Handle(GetRevenueReportQuery request, CancellationToken cancellationToken)
    {
        // Revenue come from "FeeDeduction" transactions (e.g. Platform Fee charged to seller)
        // Currently we only have "Withdrawal" type implemented in ApproveWithdrawal.
        // Assuming we will have "FeeDeduction" type when implementing order payment logic (not part of this task scope but logic needed here).
        // For now, I'll filter by 'FeeDeduction' type to show intent, even if empty.
        
        var query = _context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.Type == "FeeDeduction");

        var totalRevenue = await query
            .SumAsync(t => t.Amount, cancellationToken);

        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var revenueThisMonth = await query
            .Where(t => t.Date >= startOfMonth)
            .SumAsync(t => t.Amount, cancellationToken);
            
        var totalTransactions = await query
            .CountAsync(cancellationToken);

        // Group by day for the last 30 days
        var last30Days = DateTime.UtcNow.AddDays(-30);
        var dailyData = await query
            .Where(t => t.Type == "FeeDeduction" && t.Date >= last30Days)
            .GroupBy(t => t.Date.Date)
            .Select(g => new DailyRevenueDto(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return new RevenueReportDto
        {
            TotalRevenue = totalRevenue,
            RevenueThisMonth = revenueThisMonth,
            TotalTransactions = totalTransactions,
            DailyRevenue = dailyData
        };
    }
}
