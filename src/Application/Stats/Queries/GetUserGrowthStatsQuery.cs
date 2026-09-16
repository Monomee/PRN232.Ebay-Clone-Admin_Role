using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Stats.Queries;

public record DailyUserGrowthDto(DateTime Date, int NewBuyers, int NewSellers);

public record UserGrowthStatsDto
{
    public int TotalNewBuyers  { get; init; }
    public int TotalNewSellers { get; init; }
    public List<DailyUserGrowthDto> DailyGrowth { get; init; } = new();
}

public record GetUserGrowthStatsQuery(DateTime? StartDate, DateTime? EndDate) : IRequest<UserGrowthStatsDto>;

public class GetUserGrowthStatsQueryHandler : IRequestHandler<GetUserGrowthStatsQuery, UserGrowthStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetUserGrowthStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserGrowthStatsDto> Handle(GetUserGrowthStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsNoTracking();

        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.ToUniversalTime();
            query = query.Where(u => u.CreatedAt >= start);
        }

        if (request.EndDate.HasValue)
        {
            var end = request.EndDate.Value.ToUniversalTime();
            query = query.Where(u => u.CreatedAt <= end);
        }

        // Use CreatedAt for temporal filtering
        var usersInRange = await query
            .Select(u => new { u.CreatedAt, u.Role })
            .ToListAsync(cancellationToken);

        var grouped = usersInRange
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new DailyUserGrowthDto(
                g.Key,
                g.Count(u => u.Role == "Buyer"  || u.Role == "buyer"),
                g.Count(u => u.Role == "Seller" || u.Role == "seller")
            ))
            .OrderBy(x => x.Date)
            .ToList();

        return new UserGrowthStatsDto
        {
            TotalNewBuyers  = usersInRange.Count(u => u.Role == "Buyer"  || u.Role == "buyer"),
            TotalNewSellers = usersInRange.Count(u => u.Role == "Seller" || u.Role == "seller"),
            DailyGrowth     = grouped
        };
    }
}
