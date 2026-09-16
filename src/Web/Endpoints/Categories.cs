using EbayClone.Application.Categories.Queries.DTOs;
using EbayClone.Application.Categories.Queries.GetCategories;
using EbayClone.Application.Products.Queries.DTOs;

namespace EbayClone.Web.Endpoints;

public class Categories : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.MapGet(GetCategories);
    }

    public async Task<List<CategoryDto>> GetCategories(ISender sender)
    {
        return await sender.Send(new GetCategoriessQuery());
    }
}
