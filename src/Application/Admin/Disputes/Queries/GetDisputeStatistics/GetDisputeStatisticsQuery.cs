using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Admin.Disputes.Queries.GetDisputeStatistics;

public record GetDisputeStatisticsQuery : IRequest<DisputeStatisticsDto>;

public class DisputeStatisticsDto
{
    // Counts
    public int TotalOpen { get; init; }
    public int TotalEscalated { get; init; }
    public int TotalUnderReview { get; init; }
    public int TotalResolved { get; init; }
    public int TotalClosed { get; init; }
    
    // Urgency
    public int UrgentCases { get; init; } // Deadline < 24h
    public int CriticalPriority { get; init; }
    public int HighPriority { get; init; }
    
    // Financial
    public decimal TotalDisputedAmount { get; init; }
    public decimal TotalRefundedAmount { get; init; }
    
    // Performance
    public double AverageResolutionTimeHours { get; init; }
    public double AverageResponseTimeHours { get; init; }
    
    // Win rates (last 30 days)
    public int BuyerWins { get; init; }
    public int SellerWins { get; init; }
    public int SplitDecisions { get; init; }
    public double BuyerWinRate { get; init; }
    
    // Types breakdown
    public int INRCases { get; init; }
    public int INADCases { get; init; }
    public int DamagedCases { get; init; }
    public int CounterfeitCases { get; init; }
}

public class GetDisputeStatisticsQueryHandler : IRequestHandler<GetDisputeStatisticsQuery, DisputeStatisticsDto>
{
    private readonly IApplicationDbContext _context;

    public GetDisputeStatisticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeStatisticsDto> Handle(GetDisputeStatisticsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var urgentDeadline = now.AddHours(24);

        // Counts by status
        var totalOpen = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatuses.Open || 
                            d.Status == DisputeStatuses.AwaitingSellerResponse, 
                       cancellationToken);

        var totalEscalated = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatuses.Escalated, cancellationToken);

        var totalUnderReview = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatuses.UnderReview || 
                            d.Status == DisputeStatuses.AssignedToAdmin, 
                       cancellationToken);

        var totalResolved = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatuses.Resolved, cancellationToken);

        var totalClosed = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatuses.Closed, cancellationToken);

        // Urgency
        var urgentCases = await _context.Disputes
            .CountAsync(d => d.Deadline != null && 
                            d.Deadline <= urgentDeadline && 
                            d.Status != DisputeStatuses.Resolved && 
                            d.Status != DisputeStatuses.Closed, 
                       cancellationToken);

        var criticalPriority = await _context.Disputes
            .CountAsync(d => d.Priority == DisputePriorities.Critical && 
                            d.Status != DisputeStatuses.Closed, 
                       cancellationToken);

        var highPriority = await _context.Disputes
            .CountAsync(d => d.Priority == DisputePriorities.High && 
                            d.Status != DisputeStatuses.Closed, 
                       cancellationToken);

        // Financial
        var totalDisputedAmount = await _context.Disputes
            .Where(d => d.Status != DisputeStatuses.Resolved && 
                       d.Status != DisputeStatuses.Closed)
            .SumAsync(d => d.Amount ?? 0, cancellationToken);

        var totalRefundedAmount = await _context.Disputes
            .Where(d => d.ResolvedAt >= thirtyDaysAgo && d.RefundAmount != null)
            .SumAsync(d => d.RefundAmount ?? 0, cancellationToken);

        // Performance metrics (last 30 days)
        var resolvedCases = await _context.Disputes
            .Where(d => d.ResolvedAt >= thirtyDaysAgo && d.ResolvedAt != null)
            .ToListAsync(cancellationToken);

        var avgResolutionTime = resolvedCases.Any()
            ? resolvedCases
                .Where(d => d.CreatedAt != default && d.ResolvedAt != null)
                .Average(d => (d.ResolvedAt!.Value - d.CreatedAt).TotalHours)
            : 0;

        var avgResponseTime = resolvedCases.Any()
            ? resolvedCases
                .Where(d => d.FirstResponseAt != null)
                .Average(d => (d.FirstResponseAt!.Value - d.CreatedAt).TotalHours)
            : 0;

        // Win rates
        var buyerWins = resolvedCases.Count(d => d.Winner == DisputeWinners.Buyer);
        var sellerWins = resolvedCases.Count(d => d.Winner == DisputeWinners.Seller);
        var splitDecisions = resolvedCases.Count(d => d.Winner == DisputeWinners.Split);
        
        var totalResolutions = buyerWins + sellerWins + splitDecisions;
        var buyerWinRate = totalResolutions > 0 
            ? (double)buyerWins / totalResolutions * 100 
            : 0;

        // Types breakdown (active cases)
        var activeCases = await _context.Disputes
            .Where(d => d.Status != DisputeStatuses.Closed)
            .ToListAsync(cancellationToken);

        var inrCases = activeCases.Count(d => d.Type == DisputeTypes.INR);
        var inadCases = activeCases.Count(d => d.Type == DisputeTypes.INAD);
        var damagedCases = activeCases.Count(d => d.Type == DisputeTypes.Damaged);
        var counterfeitCases = activeCases.Count(d => d.Type == DisputeTypes.Counterfeit);

        return new DisputeStatisticsDto
        {
            TotalOpen = totalOpen,
            TotalEscalated = totalEscalated,
            TotalUnderReview = totalUnderReview,
            TotalResolved = totalResolved,
            TotalClosed = totalClosed,
            
            UrgentCases = urgentCases,
            CriticalPriority = criticalPriority,
            HighPriority = highPriority,
            
            TotalDisputedAmount = totalDisputedAmount,
            TotalRefundedAmount = totalRefundedAmount,
            
            AverageResolutionTimeHours = avgResolutionTime,
            AverageResponseTimeHours = avgResponseTime,
            
            BuyerWins = buyerWins,
            SellerWins = sellerWins,
            SplitDecisions = splitDecisions,
            BuyerWinRate = buyerWinRate,
            
            INRCases = inrCases,
            INADCases = inadCases,
            DamagedCases = damagedCases,
            CounterfeitCases = counterfeitCases
        };
    }
}
