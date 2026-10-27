using System;
using System.Threading.Tasks;
using EbayClone.Application.Common.Models;
using EbayClone.Application.SensitiveLogs;
using EbayClone.Application.SensitiveLogs.Queries.DecryptSensitiveLog;
using EbayClone.Application.SensitiveLogs.Queries.GetSensitiveLogs;
using EbayClone.Domain.Constants;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EbayClone.Web.Endpoints;

public class SensitiveLogs : EndpointGroupBase
{
    public override string GroupName => "sensitive-logs";

    public override void Map(RouteGroupBuilder groupBuilder)
    {
        // Chỉ cấp quyền cho SuperAdmin (Policies.ManageAdminRoles yêu cầu Roles.SuperAdmin)
        groupBuilder.RequireAuthorization(Policies.ManageAdminRoles);

        groupBuilder.MapGet("", GetSensitiveLogs);
        groupBuilder.MapGet("summary", GetSummary);
        groupBuilder.MapPost("{id:int}/decrypt", DecryptLog);
    }

    public async Task<Ok<PaginatedList<SensitiveLogDto>>> GetSensitiveLogs(
        ISender sender,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? actorUsername = null,
        [FromQuery] string? action = null,
        [FromQuery] string? classification = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await sender.Send(new GetSensitiveLogsQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            ActorUsername = actorUsername,
            Action = action,
            DataClassification = classification,
            SearchTerm = searchTerm,
            FromDate = fromDate,
            ToDate = toDate
        });

        return TypedResults.Ok(result);
    }

    public async Task<Ok<SensitiveLogsSummaryDto>> GetSummary(ISender sender)
    {
        var result = await sender.Send(new GetSensitiveLogsSummaryQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<DecryptedSensitiveLogResult>, NotFound>> DecryptLog(
        ISender sender,
        int id)
    {
        var result = await sender.Send(new DecryptSensitiveLogQuery(id));

        if (result == null) return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }
}
