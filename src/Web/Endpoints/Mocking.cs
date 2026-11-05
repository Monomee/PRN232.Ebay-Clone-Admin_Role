using EbayClone.Domain.Constants;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EbayClone.Application.Sellers.Commands.GenerateMockOrder;
using EbayClone.Application.Sellers.Commands.GenerateMockDispute;
using EbayClone.Application.Sellers.Commands.PushPayout;
using EbayClone.Application.Payouts.Commands.RunPayoutEngine;

namespace EbayClone.Web.Endpoints;

public class Mocking : EndpointGroupBase
{
    /// Fake orders, disputes and forced payouts are test tooling: on a real deployment they let an admin fabricate
    /// sales and move money. Off in Production unless Features:MockingEndpoints=true is set on purpose (e.g. a demo).
    public static bool Enabled(IConfiguration config, IHostEnvironment env) =>
        config.GetValue<bool?>("Features:MockingEndpoints") ?? !env.IsProduction();

    public override void Map(RouteGroupBuilder group)
    {
        var services = ((IEndpointRouteBuilder)group).ServiceProvider;
        if (!Enabled(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>())) return;

        group.RequireAuthorization(Policies.ManageUsers);
        group.MapPost("generate-seller-order", GenerateSellerOrder);
        group.MapPost("generate-mock-defect", GenerateMockDefect);
        group.MapPost("generate-mock-return-request", GenerateMockReturnRequest);
        group.MapPost("generate-mock-dispute", GenerateMockDispute);
        // These two move money (settle funds early, pay out): SuperAdmin only, not every admin who can manage users.
        group.MapPost("accelerate-settlement", AccelerateSettlement).RequireAuthorization(p => p.RequireRole(Roles.SuperAdmin));
        group.MapPost("push-payout", PushPayout).RequireAuthorization(p => p.RequireRole(Roles.SuperAdmin));
        group.MapPost("generate-mock-users", GenerateMockUsers);
    }

    public async Task<IResult> GenerateMockReturnRequest(ISender sender, [FromBody] EbayClone.Application.Sellers.Commands.GenerateMockReturnRequest.GenerateMockReturnRequestCommand command)
    {
        try
        {
            var result = await sender.Send(command);
            return TypedResults.Ok(new { success = result, message = "Mock return request generated successfully." });
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public async Task<IResult> AccelerateSettlement(ISender sender, [FromBody] EbayClone.Application.Sellers.Commands.AccelerateSettlement.AccelerateSettlementCommand command)
    {
        var count = await sender.Send(command);
        return TypedResults.Ok(new { success = true, updatedCount = count, message = $"Successfully accelerated settlement for {count} pending orders." });
    }

    public async Task<IResult> GenerateMockDefect(ISender sender, [FromBody] EbayClone.Application.Sellers.Commands.GenerateMockDefect.GenerateMockDefectCommand command)
    {
        try
        {
            var result = await sender.Send(command);
            return TypedResults.Ok(new { success = result, message = "Mock defect generated successfully." });
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public async Task<IResult> GenerateMockDispute(ISender sender, [FromBody] GenerateMockDisputeCommand command)
    {
        try
        {
            var result = await sender.Send(command);
            return TypedResults.Ok(new { success = result, message = "Mock dispute generated successfully." });
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public async Task<IResult> GenerateSellerOrder(ISender sender, [FromBody] GenerateMockOrderCommand command)
    {
        try
        {
            var result = await sender.Send(command);
            return TypedResults.Ok(new { success = result, message = "Mock order generated." });
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public async Task<IResult> PushPayout(ISender sender, [FromBody] PushPayoutCommand command)
    {
        try
        {
            var result = await sender.Send(command);
            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public async Task<IResult> GenerateMockUsers(ISender sender, [FromBody] EbayClone.Application.Users.Commands.GenerateMockUsers.GenerateMockUsersCommand command)
    {
         var result = await sender.Send(command);
         return TypedResults.Ok(new { success = result, message = "Mock users generated successfully." });
    }
}
