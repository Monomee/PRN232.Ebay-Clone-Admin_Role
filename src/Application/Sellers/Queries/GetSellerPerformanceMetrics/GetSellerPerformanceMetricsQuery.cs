using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Queries.GetSellerPerformanceMetrics;

public record SellerPerformanceMetricsDto
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string SellerLevel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int TransactionCount { get; init; }
    public decimal TotalSales { get; init; }
    public int UnresolvedCases { get; init; }
    public double DefectRate { get; init; }
    public double LateRate { get; init; }
    public int ActiveDays { get; init; }
}

public record GetSellerPerformanceMetricsQuery : IRequest<List<SellerPerformanceMetricsDto>>;

public class GetSellerPerformanceMetricsQueryHandler : IRequestHandler<GetSellerPerformanceMetricsQuery, List<SellerPerformanceMetricsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSellerPerformanceMetricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SellerPerformanceMetricsDto>> Handle(GetSellerPerformanceMetricsQuery request, CancellationToken cancellationToken)
    {
        var sellers = await _context.Users
            .Where(u => u.Role == "Seller" || _context.Stores.Any(s => s.SellerId == u.Id))
            .ToListAsync(cancellationToken);

        var metricsList = new List<SellerPerformanceMetricsDto>();
        var now = DateTime.UtcNow;

        foreach (var seller in sellers)
        {
            var threeMonthsAgo = now.AddMonths(-3);
            var threeMonthOrders = await _context.OrderTables
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && o.OrderDate >= threeMonthsAgo)
                .ToListAsync(cancellationToken);

            int transactionCount = threeMonthOrders.Count;
            var evaluationPeriod = threeMonthsAgo;

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

            var totalSales = threeMonthOrders.Sum(o => o.TotalPrice ?? 0);

            var unresolvedCases = await _context.Disputes
                .Where(d => d.Order != null && d.Order.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id) && d.CreatedAt >= evaluationPeriod && 
                            (d.Status == "ClosedWithoutResolution" || d.Status == "Escalated"))
                .CountAsync(cancellationToken);

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

            metricsList.Add(new SellerPerformanceMetricsDto
            {
                Id = seller.Id,
                Username = string.IsNullOrEmpty(seller.Username) ? seller.Email ?? "Unknown" : seller.Username,
                SellerLevel = seller.SellerLevel,
                Status = seller.Status,
                TransactionCount = transactionCount,
                TotalSales = totalSales,
                UnresolvedCases = unresolvedCases,
                DefectRate = defectRate,
                LateRate = lateRate,
                ActiveDays = daysSinceFirstOrder
            });
        }

        return metricsList;
    }
}
