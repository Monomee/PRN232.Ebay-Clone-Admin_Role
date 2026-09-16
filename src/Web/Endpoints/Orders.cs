using EbayClone.Application.Common.Models;
using EbayClone.Application.Orders.Queries.GetOrdersWithPagination;
using EbayClone.Application.Orders.Queries.GetOrderDetail;
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
        group.RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapGet("", GetOrders);
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
