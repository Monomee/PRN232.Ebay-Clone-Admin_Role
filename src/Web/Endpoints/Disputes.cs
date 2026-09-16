using EbayClone.Application.Admin.Disputes.Commands.AssignDispute;
using EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;
using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeDetail;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeDocket;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeStatistics;
using EbayClone.Application.Common.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EbayClone.Web.Endpoints;

public class Disputes : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet("statistics", GetStatistics)
            .WithName("GetDisputeStatistics")
            .WithTags("Disputes - Admin")
            .Produces<DisputeStatisticsDto>(StatusCodes.Status200OK);

        groupBuilder.MapGet("", GetDisputeDocket)
            .WithName("GetDisputeDocket")
            .WithTags("Disputes - Admin")
            .Produces<PaginatedList<DisputeDto>>(StatusCodes.Status200OK);

        groupBuilder.MapGet("{id:int}", GetDisputeDetail)
            .WithName("GetDisputeDetail")
            .WithTags("Disputes - Admin")
            .Produces<DisputeDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        groupBuilder.MapPost("{id:int}/assign", AssignDispute)
            .WithName("AssignDispute")
            .WithTags("Disputes - Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        groupBuilder.MapPost("{id:int}/resolve", ResolveDispute)
            .WithName("ResolveDispute")
            .WithTags("Disputes - Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    // GET /api/disputes/statistics
    public async Task<Ok<DisputeStatisticsDto>> GetStatistics(ISender sender)
    {
        var statistics = await sender.Send(new GetDisputeStatisticsQuery());
        return TypedResults.Ok(statistics);
    }

    // GET /api/disputes?status=Open&priority=High&pageNumber=1&pageSize=10
    public async Task<Ok<PaginatedList<DisputeDto>>> GetDisputeDocket(
        ISender sender,
        [AsParameters] GetDisputeDocketQuery query)
    {
        var disputes = await sender.Send(query);
        return TypedResults.Ok(disputes);
    }

    // GET /api/disputes/123
    public async Task<Results<Ok<DisputeDetailDto>, NotFound>> GetDisputeDetail(
        ISender sender,
        int id)
    {
        try
        {
            var dispute = await sender.Send(new GetDisputeDetailQuery(id));
            return TypedResults.Ok(dispute);
        }
        catch (Exception)
        {
            return TypedResults.NotFound();
        }
    }

    // POST /api/disputes/123/assign
    public async Task<Results<Ok, BadRequest<string>, NotFound>> AssignDispute(
        ISender sender,
        int id)
    {
        try
        {
            await sender.Send(new AssignDisputeCommand(id));
            return TypedResults.Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return TypedResults.NotFound();
        }
    }

    // POST /api/disputes/123/resolve
    public async Task<Results<Ok, BadRequest<string>, NotFound>> ResolveDispute(
        ISender sender,
        int id,
        [Microsoft.AspNetCore.Mvc.FromBody] ResolveDisputeCommand command)
    {
        // Ensure the ID in the route matches the command
        if (command.DisputeId != id)
        {
            return TypedResults.BadRequest("Dispute ID mismatch");
        }

        try
        {
            await sender.Send(command);
            return TypedResults.Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return TypedResults.NotFound();
        }
    }
}
