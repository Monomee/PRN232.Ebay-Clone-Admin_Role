using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Stats.Queries;

public record AdminDashboardStatsDto
{
    public decimal DailyRevenue { get; init; }
    public decimal MonthlyRevenue { get; init; }
    public decimal QuarterlyRevenue { get; init; }
    public int TotalUsers { get; init; }
    public int NewUsersThisMonth { get; init; }
}

public record GetAdminDashboardStatsQuery : IRequest<AdminDashboardStatsDto>;

public class GetAdminDashboardStatsQueryHandler : IRequestHandler<GetAdminDashboardStatsQuery, AdminDashboardStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDashboardStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardStatsDto> Handle(GetAdminDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfQuarter = new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1);

        var dailyRevenue = await _context.FinancialTransactions
            .Where(t => t.Type == "FeeDeduction" && t.Date >= startOfDay)
            .SumAsync(t => t.Amount, cancellationToken);

        var monthlyRevenue = await _context.FinancialTransactions
            .Where(t => t.Type == "FeeDeduction" && t.Date >= startOfMonth)
            .SumAsync(t => t.Amount, cancellationToken);

        var quarterlyRevenue = await _context.FinancialTransactions
            .Where(t => t.Type == "FeeDeduction" && t.Date >= startOfQuarter)
            .SumAsync(t => t.Amount, cancellationToken);

        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        
        // Since 'CreatedAt' is missing in User entity, using 'ApprovedAt' as a fallback if available,
        // or just returning total users for now if we can't determine "new" users reliably.
        // Actually, I'll check if I can find any date field in User.
        
        var newUsersThisMonth = await _context.Users
            .Where(u => u.ApprovedAt >= startOfMonth) // Fallback to ApprovedAt
            .CountAsync(cancellationToken);

        return new AdminDashboardStatsDto
        {
            DailyRevenue = dailyRevenue,
            MonthlyRevenue = monthlyRevenue,
            QuarterlyRevenue = quarterlyRevenue,
            TotalUsers = totalUsers,
            NewUsersThisMonth = newUsersThisMonth
        };
    }
}
