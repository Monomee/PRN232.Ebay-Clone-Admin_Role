using System.Collections.Generic;

namespace EbayClone.Application.Dashboard.Queries.GetDashboardMetrics;

public class DashboardMetricsDto
{
    public int TotalUsers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalOrdersToday { get; set; }

    public int PendingAccountsCount { get; set; }
    public int OpenDisputesCount { get; set; }
    public int NewReturnRequestsCount { get; set; }

    public List<DailyRevenueDto> WeeklyRevenue { get; set; } = new();
}

public class DailyRevenueDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}
