using EbayClone.Application.Users.Commands.UpdateBankAccount;

namespace EbayClone.Web.Endpoints;

/// Self-service endpoints of the signed-in seller (/api/Account).
public class Account : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization(policy => policy.RequireRole("Seller"));
        group.MapPut("bank-account", async (ISender sender, UpdateBankAccountCommand command) =>
        {
            await sender.Send(command);
            return Results.NoContent();
        });
    }
}
