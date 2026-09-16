using EbayClone.Application.Common.Models;
using EbayClone.Application.Reviews;
using EbayClone.Application.Reviews.Commands;
using EbayClone.Application.Reviews.Queries;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EbayClone.Web.Endpoints;

public class Reviews : EndpointGroupBase
{
    public override string GroupName => "reviews";

    public override void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetFlaggedReviews, "flagged");
        groupBuilder.MapPut(UpdateReviewStatus, "{id:int}/status");
        groupBuilder.MapPost(CreateReview);
        groupBuilder.MapPost(ReplyToReview, "{id:int}/reply");
        groupBuilder.MapPost(ReportReview, "{id:int}/report");
    }

    public async Task<Ok<int>> CreateReview(ISender sender, [FromBody] CreateReviewCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<ReviewModerationDto>>> GetFlaggedReviews(
        ISender sender,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await sender.Send(new GetFlaggedReviewsQuery 
        { 
            PageNumber = pageNumber, 
            PageSize = pageSize 
        });
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok, NotFound>> UpdateReviewStatus(
        ISender sender,
        int id,
        [FromBody] UpdateReviewStatusRequest request)
    {
        var result = await sender.Send(new UpdateReviewStatusCommand
        {
            Id = id,
            Status = request.Status,
            Action = request.Action,
            AdminId = request.AdminId
        });

        if (!result) return TypedResults.NotFound();

        return TypedResults.Ok();
    }

    public async Task<Results<Ok, NotFound>> ReplyToReview(
        ISender sender,
        int id,
        [FromBody] ReplyToReviewRequest request)
    {
        var result = await sender.Send(new ReplyToReviewCommand
        {
            ReviewId = id,
            SellerId = request.SellerId,
            Reply = request.Reply
        });

        if (!result) return TypedResults.NotFound();

        return TypedResults.Ok();
    }

    public async Task<Results<Ok, NotFound>> ReportReview(
        ISender sender,
        int id,
        [FromBody] ReportReviewRequest request)
    {
        var result = await sender.Send(new ReportReviewCommand
        {
            ReviewId = id,
            ReporterUserId = request.ReporterId,
            Reason = request.Reason,
            Description = request.Description
        });

        if (!result) return TypedResults.NotFound();

        return TypedResults.Ok();
    }
}

public record UpdateReviewStatusRequest(string Status, string Action, int AdminId);

public record ReplyToReviewRequest(int SellerId, string Reply);

public record ReportReviewRequest(int? ReporterId, string Reason, string? Description);
