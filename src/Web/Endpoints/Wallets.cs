using EbayClone.Application.Financials.Queries.GetSellerWallets;
using EbayClone.Domain.Constants;
using MediatR;

namespace EbayClone.Web.Endpoints;

public class Wallets : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapGet("/", GetWallets);
    }

    public async Task<List<SellerWalletDto>> GetWallets(ISender sender)
    {
        return await sender.Send(new GetSellerWalletsQuery());
    }
}
