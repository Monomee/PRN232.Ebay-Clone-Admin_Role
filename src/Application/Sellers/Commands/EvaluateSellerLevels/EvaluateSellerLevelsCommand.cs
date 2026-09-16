using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Commands.EvaluateSellerLevels;

public record EvaluateSellerLevelsCommand : IRequest<int>;

public class EvaluateSellerLevelsCommandHandler : IRequestHandler<EvaluateSellerLevelsCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ISellerHubService _sellerHubService;
    private readonly ISender _sender;

    public EvaluateSellerLevelsCommandHandler(IApplicationDbContext context, ISellerHubService sellerHubService, ISender sender)
    {
        _context = context;
        _sellerHubService = sellerHubService;
        _sender = sender;
    }

    public async Task<int> Handle(EvaluateSellerLevelsCommand request, CancellationToken cancellationToken)
    {
        var sellers = await _context.Users
            .Where(u => u.Role == "Seller" || _context.Stores.Any(s => s.SellerId == u.Id))
            .ToListAsync(cancellationToken);

        int updatedCount = 0;
        var now = DateTime.UtcNow;

        var criteria = await _context.SellerLevelCriteria.FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);
        if (criteria == null)
        {
            criteria = new SellerLevelCriteria(); // Use defaults if not found
        }

        var updatedSellerIds = new List<int>();

        foreach (var seller in sellers)
        {
            // Calculate total transactions in last 3 months
            var threeMonthsAgo = now.AddMonths(-3);
            var threeMonthOrders = await _context.OrderTables
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && o.OrderDate >= threeMonthsAgo)
                .ToListAsync(cancellationToken);

            int transactionCount = threeMonthOrders.Count;
            var evaluationPeriod = threeMonthsAgo;

            // If < 400 transactions in 3 months, look back 12 months
            if (transactionCount < 400)
            {
                evaluationPeriod = now.AddMonths(-12);
                threeMonthOrders = await _context.OrderTables
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && o.OrderDate >= evaluationPeriod)
                    .ToListAsync(cancellationToken);
                
                transactionCount = threeMonthOrders.Count;
            }

            // Sales Volume
            var totalSales = threeMonthOrders.Sum(o => o.TotalPrice ?? 0);

            // Metrics
            var unresolvedCases = await _context.Disputes
                .Where(d => d.Order != null && d.Order.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && d.CreatedAt >= evaluationPeriod && 
                            (d.Status == "ClosedWithoutResolution" || d.Status == "Escalated"))
                .CountAsync(cancellationToken);

            double unresolvedRate = transactionCount > 0 ? (double)unresolvedCases / transactionCount : 0;

            var returnDefects = await _context.ReturnRequests
                .Include(r => r.Order)
                .ThenInclude(o => o!.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(r => r.Order != null && r.Order.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && 
                            r.CreatedAt >= evaluationPeriod && r.Status == "Refunded")
                .CountAsync(cancellationToken);
            
            var totalDefects = unresolvedCases + returnDefects; 
            double defectRate = transactionCount > 0 ? (double)totalDefects / transactionCount : 0;

            var lateShipments = threeMonthOrders.Count(o => o.CompletedAt.HasValue && o.OrderDate.HasValue && 
                                                            (o.CompletedAt.Value - o.OrderDate.Value).TotalDays > 7);
            double lateRate = transactionCount > 0 ? (double)lateShipments / transactionCount : 0;

            // Evaluate Level using dynamic criteria
            // Calculate days since first successful order
            var firstOrder = await _context.OrderTables
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && o.Status == "Completed")
                .OrderBy(o => o.OrderDate)
                .FirstOrDefaultAsync(cancellationToken);

            int daysSinceFirstOrder = 0;
            if (firstOrder?.OrderDate != null)
            {
                daysSinceFirstOrder = (int)(now - firstOrder.OrderDate.Value).TotalDays;
            }

            string newLevel = "BelowStandard";

            // Is Top Rated?
            bool isTopRated = transactionCount >= criteria.TopRatedMinTransactions && 
                              totalSales >= criteria.TopRatedMinSales && 
                              (now - (seller.ApprovedAt ?? now.AddDays(-1000))).TotalDays >= criteria.TopRatedMinDays &&
                              unresolvedCases <= criteria.TopRatedMaxUnresolvedCases &&
                              defectRate <= criteria.TopRatedMaxDefectRate &&
                              lateRate <= criteria.TopRatedMaxLateRate;

            // Is Above Standard?
            bool isAboveStandard = !isTopRated && 
                                   transactionCount > 0 && 
                                   daysSinceFirstOrder >= criteria.AboveStandardMinDays &&
                                   defectRate <= criteria.AboveStandardMaxDefectRate && 
                                   (unresolvedCases <= criteria.AboveStandardMaxUnresolvedCases || unresolvedRate <= criteria.AboveStandardMaxUnresolvedRate);

            if (isTopRated) newLevel = "TopRated";
            else if (isAboveStandard) newLevel = "AboveStandard";

            if (seller.SellerLevel != newLevel)
            {
                seller.SellerLevel = newLevel;
                updatedSellerIds.Add(seller.Id);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);

            var metricsList = await _sender.Send(new EbayClone.Application.Sellers.Queries.GetSellerPerformanceMetrics.GetSellerPerformanceMetricsQuery(), cancellationToken);
            var updatedMetrics = metricsList.Where(m => updatedSellerIds.Contains(m.Id));
            foreach(var metric in updatedMetrics)
            {
                await _sellerHubService.BroadcastSellerMetricsUpdate(metric);
            }
        }

        return updatedCount;
    }
}
