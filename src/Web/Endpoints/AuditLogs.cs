using EbayClone.Application.AuditLogs;
using EbayClone.Application.AuditLogs.Queries.GetAuditLogs;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Constants;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EbayClone.Web.Endpoints;

public class AuditLogs : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization(Policies.ViewAuditLogs);

        groupBuilder.MapGet("", GetAuditLogs);
        groupBuilder.MapGet("admin/{adminId:int}", GetAuditLogsByAdmin);
        groupBuilder.MapGet("entity/{targetType}/{targetId:int}", GetAuditLogsByEntity);
    }

    public async Task<Ok<PaginatedList<AuditLogDto>>> GetAuditLogs(
        ISender sender,
        [AsParameters] GetAuditLogsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<AuditLogDto>>> GetAuditLogsByAdmin(
        ISender sender,
        int adminId,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var result = await sender.Send(new GetAuditLogsQuery
        {
            AdminId = adminId,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<AuditLogDto>>> GetAuditLogsByEntity(
        ISender sender,
        string targetType,
        int targetId,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var result = await sender.Send(new GetAuditLogsQuery
        {
            TargetType = targetType,
            TargetId = targetId,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
        return TypedResults.Ok(result);
    }
}
