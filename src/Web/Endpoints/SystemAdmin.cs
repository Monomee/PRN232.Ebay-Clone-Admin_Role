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
    }

    public Ok<IEnumerable<ActiveConnection>> GetActiveIps(IActiveConnectionTracker tracker)
    {
        // Retrieve connections active in the last 60 minutes
        var activeConnections = tracker.GetActiveConnections(TimeSpan.FromMinutes(60));
        return TypedResults.Ok(activeConnections);
    }
}
