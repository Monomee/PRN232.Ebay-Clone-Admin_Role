using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Criteria;

public record SellerLevelCriteriaDto
{
    public int TopRatedMinTransactions { get; init; }
    public decimal TopRatedMinSales { get; init; }
    public int TopRatedMinDays { get; init; }
    public int TopRatedMaxUnresolvedCases { get; init; }
    public double TopRatedMaxDefectRate { get; init; }
    public double TopRatedMaxLateRate { get; init; }
    public int AboveStandardMinDays { get; init; }
    public double AboveStandardMaxDefectRate { get; init; }
    public int AboveStandardMaxUnresolvedCases { get; init; }
    public double AboveStandardMaxUnresolvedRate { get; init; }
    public DateTime NextEvaluationDate { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record GetSellerLevelCriteriaQuery : IRequest<SellerLevelCriteriaDto>;

public class GetSellerLevelCriteriaQueryHandler : IRequestHandler<GetSellerLevelCriteriaQuery, SellerLevelCriteriaDto>
{
    private readonly IApplicationDbContext _context;

    public GetSellerLevelCriteriaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SellerLevelCriteriaDto> Handle(GetSellerLevelCriteriaQuery request, CancellationToken cancellationToken)
    {
        var criteria = await _context.SellerLevelCriteria.FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);
        
        if (criteria == null)
        {
            // Return defaults if not seeded yet
            return new SellerLevelCriteriaDto
            {
                TopRatedMinTransactions = 100,
                TopRatedMinSales = 1000m,
                TopRatedMinDays = 90,
                TopRatedMaxUnresolvedCases = 2,
                TopRatedMaxDefectRate = 0.005,
                TopRatedMaxLateRate = 0.03,
                AboveStandardMinDays = 30,
                AboveStandardMaxDefectRate = 0.02,
                AboveStandardMaxUnresolvedCases = 2,
                AboveStandardMaxUnresolvedRate = 0.003,
                NextEvaluationDate = DateTime.UtcNow.AddMonths(1),
                UpdatedAt = DateTime.UtcNow
            };
        }

        return new SellerLevelCriteriaDto
        {
            TopRatedMinTransactions = criteria.TopRatedMinTransactions,
            TopRatedMinSales = criteria.TopRatedMinSales,
            TopRatedMinDays = criteria.TopRatedMinDays,
            TopRatedMaxUnresolvedCases = criteria.TopRatedMaxUnresolvedCases,
            TopRatedMaxDefectRate = criteria.TopRatedMaxDefectRate,
            TopRatedMaxLateRate = criteria.TopRatedMaxLateRate,
            AboveStandardMinDays = criteria.AboveStandardMinDays,
            AboveStandardMaxDefectRate = criteria.AboveStandardMaxDefectRate,
            AboveStandardMaxUnresolvedCases = criteria.AboveStandardMaxUnresolvedCases,
            AboveStandardMaxUnresolvedRate = criteria.AboveStandardMaxUnresolvedRate,
            NextEvaluationDate = criteria.NextEvaluationDate,
            UpdatedAt = criteria.UpdatedAt
        };
    }
}
