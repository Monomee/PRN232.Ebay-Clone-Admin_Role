using EbayClone.Application.Common.Models;
using EbayClone.Application.Orders.Queries.GetOrdersWithPagination;
using EbayClone.Application.Orders.Queries.GetOrderDetail;
using EbayClone.Application.Orders.Queries.GetOrderSummary;
using EbayClone.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using EbayClone.Web.Infrastructure;

namespace EbayClone.Web.Endpoints;

public class Orders : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.ManageOrders);

        group.MapGet("", GetOrders);
        group.MapGet("summary", (ISender sender, [AsParameters] GetOrdersWithPaginationQuery filter) => sender.Send(new GetOrderSummaryQuery(filter)));
        group.MapGet("{id:int}", GetOrderById);
    }

    public async Task<Ok<PaginatedList<OrderBriefDto>>> GetOrders(
        ISender sender,
        [AsParameters] GetOrdersWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<OrderDetailDto>, NotFound>> GetOrderById(
        ISender sender,
        int id)
    {
        var result = await sender.Send(new GetOrderDetailQuery(id));
        
        if (result == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result);
    }
}
