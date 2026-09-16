using EbayClone.Application.Broadcasts;
using EbayClone.Application.Broadcasts.Commands.SendBroadcast;
using EbayClone.Application.Broadcasts.Queries.GetBroadcasts;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Constants;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EbayClone.Web.Endpoints;

public class Broadcasts : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder groupBuilder)
    {
        // Admin endpoints group with ManageBroadcasts policy
        var manageGroup = groupBuilder.MapGroup("").RequireAuthorization(Policies.ManageBroadcasts);
        manageGroup.MapGet("", GetBroadcasts);
        manageGroup.MapPost("", SendBroadcast);
        manageGroup.MapPost("schedule", ScheduleBroadcast);

        // New endpoint for users to read active broadcasts:
        var activeGroup = groupBuilder.MapGroup("active").RequireAuthorization(); // Any authenticated user can access
        activeGroup.MapGet("", GetActiveBroadcasts);
        activeGroup.MapPost("{id}/read", MarkAsRead);
    }

    public async Task<Results<Ok<bool>, BadRequest<string>>> MarkAsRead(
        [FromRoute] int id,
        ISender sender)
    {
        var result = await sender.Send(new EbayClone.Application.Notifications.Commands.MarkAsRead.MarkNotificationAsReadCommand(id));
        return result ? TypedResults.Ok(true) : TypedResults.BadRequest("Failed to mark notification as read");
    }

    public async Task<Ok<List<BroadcastDto>>> GetActiveBroadcasts(
        ISender sender,
        ClaimsPrincipal user)
    {
        // Get the user's roles from JWT claims
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var result = await sender.Send(new EbayClone.Application.Broadcasts.Queries.GetActiveBroadcasts.GetActiveBroadcastsQuery(roles));
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<BroadcastDto>>> GetBroadcasts(
        ISender sender,
        [AsParameters] GetBroadcastsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest<string>>> SendBroadcast(
        ISender sender,
        [FromBody] SendBroadcastRequest request)
    {
        try
        {
            var count = await sender.Send(new SendBroadcastCommand
            {
                Title = request.Title,
                Content = request.Content,
                TargetAudience = request.TargetAudience,
                TargetGroup = request.TargetGroup,
                Channels = request.Channels,
                ScheduleAt = null, // Send immediately
                CreatedBy = request.CreatedBy
            });

            return TypedResults.Created($"/api/Broadcasts", count);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public async Task<Results<Created<int>, BadRequest<string>>> ScheduleBroadcast(
        ISender sender,
        [FromBody] ScheduleBroadcastRequest request)
    {
        try
        {
            var count = await sender.Send(new SendBroadcastCommand
            {
                Title = request.Title,
                Content = request.Content,
                TargetAudience = request.TargetAudience,
                TargetGroup = request.TargetGroup,
                Channels = request.Channels,
                ScheduleAt = request.ScheduleAt,
                CreatedBy = request.CreatedBy
            });

            return TypedResults.Created($"/api/Broadcasts", count);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }
}

// Request DTOs
public record SendBroadcastRequest(
    string Title,
    string Content,
    string TargetAudience,
    string? TargetGroup,
    List<string> Channels,
    int CreatedBy);

public record ScheduleBroadcastRequest(
    string Title,
    string Content,
    string TargetAudience,
    string? TargetGroup,
    List<string> Channels,
    DateTime ScheduleAt,
    int CreatedBy);
