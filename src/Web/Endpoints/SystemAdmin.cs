using EbayClone.Application.SystemAdmin.IpBlocks;
using EbayClone.Domain.Constants;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Generic;
using System;

namespace EbayClone.Web.Endpoints;

public class SystemAdmin : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin));
        
        group.MapGet("active-ips", GetActiveIps);

        // Sign-up blocklist (IpBlocklist): automatic blocks from repeated bans plus manual ones; lifting keeps the row.
        group.MapGet("ip-blocks", (ISender sender) => sender.Send(new GetIpBlocksQuery()));
        group.MapPost("ip-blocks", async (ISender sender, BlockIpCommand command) => Results.Ok(new { id = await sender.Send(command) }));
        group.MapPost("ip-blocks/{id:int}/lift", async (ISender sender, int id) =>
            await sender.Send(new LiftIpBlockCommand(id)) ? Results.NoContent() : Results.NotFound());
    }

    public Ok<IEnumerable<ActiveConnection>> GetActiveIps(IActiveConnectionTracker tracker)
    {
        // Retrieve connections active in the last 60 minutes
        var activeConnections = tracker.GetActiveConnections(TimeSpan.FromMinutes(60));
        return TypedResults.Ok(activeConnections);
    }
}
