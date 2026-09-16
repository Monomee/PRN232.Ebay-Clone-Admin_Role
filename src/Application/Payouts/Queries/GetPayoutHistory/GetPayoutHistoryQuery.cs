using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Payouts.Queries.GetPayoutHistory;

public record GetPayoutHistoryQuery(string GroupBy = "day") : IRequest<List<PayoutHistoryDto>>;

public record PayoutHistoryDto(string Label, decimal TotalAmount, int Count);

public class GetPayoutHistoryQueryHandler : IRequestHandler<GetPayoutHistoryQuery, List<PayoutHistoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPayoutHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PayoutHistoryDto>> Handle(GetPayoutHistoryQuery request, CancellationToken cancellationToken)
    {
        var successTx = await _context.PayoutTransactions
            .Where(t => t.Status == "Success")
            .ToListAsync(cancellationToken);

        if (request.GroupBy.Equals("month", StringComparison.OrdinalIgnoreCase))
        {
            return successTx
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                .Select(g => new PayoutHistoryDto(
                    Label: $"{g.Key.Year}-{g.Key.Month:D2}",
                    TotalAmount: g.Sum(t => t.Amount),
                    Count: g.Count()))
                .OrderBy(d => d.Label)
                .ToList();
        }

        // Default: group by day
        return successTx
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new PayoutHistoryDto(
                Label: g.Key.ToString("yyyy-MM-dd"),
                TotalAmount: g.Sum(t => t.Amount),
                Count: g.Count()))
            .OrderBy(d => d.Label)
            .ToList();
    }
}
