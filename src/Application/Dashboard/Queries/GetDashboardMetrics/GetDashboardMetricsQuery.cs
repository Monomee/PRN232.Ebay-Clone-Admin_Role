using EbayClone.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Dashboard.Queries.GetDashboardMetrics;

public record GetDashboardMetricsQuery : IRequest<DashboardMetricsDto>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public class GetDashboardMetricsQueryHandler : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly IApplicationDbContext _context;

    public GetDashboardMetricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
        {
            throw new InvalidOperationException("End date must be greater than or equal to start date.");
        }

        var now = DateTime.UtcNow;
        var today = now.Date;
        var sevenDaysAgo = today.AddDays(-6);

        DateTime? startDate = request.StartDate.HasValue ? DateRange.ToUtc(request.StartDate.Value) : null;
        DateTime? endDate = request.EndDate.HasValue ? DateRange.ExclusiveEnd(request.EndDate.Value) : null;

        var totalUsers = await _context.Users
            .CountAsync(u => (!startDate.HasValue || u.CreatedAt >= startDate.Value) &&
                             (!endDate.HasValue || u.CreatedAt < endDate.Value), 
                        cancellationToken);

        var totalProducts = await _context.Products
            .CountAsync(p => (!startDate.HasValue || p.CreatedAt >= startDate.Value) &&
                             (!endDate.HasValue || p.CreatedAt < endDate.Value), 
                        cancellationToken);
        
        var totalOrders = await _context.OrderTables
            .CountAsync(o => (!startDate.HasValue || o.OrderDate >= startDate.Value) &&
                             (!endDate.HasValue || o.OrderDate < endDate.Value), 
                        cancellationToken);

        var pendingAccountsCount = await _context.Users
            .CountAsync(u => u.ApprovalStatus == "PendingApproval" || u.Status == "Pending", cancellationToken);

        var openDisputesCount = await _context.Disputes
            .CountAsync(d => d.Status == DisputeStatuses.Open || 
                             d.Status == DisputeStatuses.AwaitingSellerResponse ||
                             d.Status == DisputeStatuses.Escalated ||
                             d.Status == DisputeStatuses.UnderReview ||
                             d.Status == DisputeStatuses.AssignedToAdmin, 
                        cancellationToken);

        var newReturnRequestsCount = await _context.ReturnRequests
            .CountAsync(r => r.Status == "Pending", cancellationToken);

        // Weekly Revenue Chart Data: platform revenue is the fees charged (same source as the revenue report),
        // not the gross order value that goes to sellers.
        var feesLast7Days = await _context.FinancialTransactions
            .Where(t => t.Type == "FeeDeduction" && t.Date >= sevenDaysAgo)
            .Select(t => new { t.Date, t.Amount })
            .ToListAsync(cancellationToken);

        var weeklyRevenue = new List<DailyRevenueDto>();
        for (int i = 0; i < 7; i++)
        {
            var date = sevenDaysAgo.AddDays(i);
            var dailyTotal = feesLast7Days
                .Where(t => t.Date.Date == date)
                .Sum(t => t.Amount);

            weeklyRevenue.Add(new DailyRevenueDto
            {
                Date = date.ToString("yyyy-MM-dd"),
                Revenue = dailyTotal
            });
        }

        return new DashboardMetricsDto
        {
            TotalUsers = totalUsers,
            TotalProducts = totalProducts,
            TotalOrdersToday = totalOrders, // Renamed logic-wise to fit the range
            PendingAccountsCount = pendingAccountsCount,
            OpenDisputesCount = openDisputesCount,
            NewReturnRequestsCount = newReturnRequestsCount,
            WeeklyRevenue = weeklyRevenue
        };
    }
}
