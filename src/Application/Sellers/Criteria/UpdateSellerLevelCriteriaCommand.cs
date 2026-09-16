using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Criteria;

public record UpdateSellerLevelCriteriaCommand : IRequest<bool>
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
    public DateTime? NextEvaluationDate { get; init; }
}

public class UpdateSellerLevelCriteriaCommandHandler : IRequestHandler<UpdateSellerLevelCriteriaCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateSellerLevelCriteriaCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateSellerLevelCriteriaCommand request, CancellationToken cancellationToken)
    {
        var criteria = await _context.SellerLevelCriteria.FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);
        
        if (criteria == null)
        {
            criteria = new SellerLevelCriteria();
            _context.SellerLevelCriteria.Add(criteria);
        }

        criteria.TopRatedMinTransactions = request.TopRatedMinTransactions;
        criteria.TopRatedMinSales = request.TopRatedMinSales;
        criteria.TopRatedMinDays = request.TopRatedMinDays;
        criteria.TopRatedMaxUnresolvedCases = request.TopRatedMaxUnresolvedCases;
        criteria.TopRatedMaxDefectRate = request.TopRatedMaxDefectRate;
        criteria.TopRatedMaxLateRate = request.TopRatedMaxLateRate;
        criteria.AboveStandardMinDays = request.AboveStandardMinDays;
        criteria.AboveStandardMaxDefectRate = request.AboveStandardMaxDefectRate;
        criteria.AboveStandardMaxUnresolvedCases = request.AboveStandardMaxUnresolvedCases;
        criteria.AboveStandardMaxUnresolvedRate = request.AboveStandardMaxUnresolvedRate;
        
        if (request.NextEvaluationDate.HasValue)
        {
            criteria.NextEvaluationDate = request.NextEvaluationDate.Value;
        }

        criteria.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
