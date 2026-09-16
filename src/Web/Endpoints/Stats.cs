using EbayClone.Application.Stats.Queries;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EbayClone.Web.Endpoints;

public class Stats : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet("dashboard", GetDashboardStats);
        groupBuilder.MapGet("reports/revenue", GetRevenueStats);
        groupBuilder.MapGet("reports/users",   GetUserGrowthStats);
        groupBuilder.MapGet("reports/orders",  GetOrderStats);
    }

    public async Task<Ok<AdminDashboardStatsDto>> GetDashboardStats(ISender sender)
    {
        var result = await sender.Send(new GetAdminDashboardStatsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<RevenueStatsDto>> GetRevenueStats(
        ISender sender,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await sender.Send(new GetRevenueStatsQuery(startDate, endDate));
        return TypedResults.Ok(result);
    }

    public async Task<Ok<UserGrowthStatsDto>> GetUserGrowthStats(
        ISender sender,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await sender.Send(new GetUserGrowthStatsQuery(startDate, endDate));
        return TypedResults.Ok(result);
    }

    public async Task<Ok<OrderStatsDto>> GetOrderStats(
        ISender sender,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await sender.Send(new GetOrderStatsQuery(startDate, endDate));
        return TypedResults.Ok(result);
    }
}
