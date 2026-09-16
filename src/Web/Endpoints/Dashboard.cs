using EbayClone.Application.Dashboard.Queries.GetDashboardMetrics;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EbayClone.Web.Endpoints;

public class Dashboard : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("metrics", GetDashboardMetrics)
            .WithName("GetDashboardMetrics")
            .WithTags("Dashboard")
            .Produces<DashboardMetricsDto>(StatusCodes.Status200OK);
    }

    public async Task<Ok<DashboardMetricsDto>> GetDashboardMetrics(
        ISender sender, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        var result = await sender.Send(new GetDashboardMetricsQuery 
        { 
            StartDate = startDate, 
            EndDate = endDate 
        });
        return TypedResults.Ok(result);
    }
}
