using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Application.Reviews;
using EbayClone.Application.Reviews.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Queries;

public record GetFlaggedReviewsQuery : IRequest<PaginatedList<ReviewModerationDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetFlaggedReviewsQueryHandler : IRequestHandler<GetFlaggedReviewsQuery, PaginatedList<ReviewModerationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetFlaggedReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ReviewModerationDto>> Handle(GetFlaggedReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Reviews
            .AsNoTracking()
            .Where(r => r.FlaggedBySystem || r.Status == "PendingReview" || r.Status == "Hidden")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewModerationDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductTitle = r.Product != null ? r.Product.Title : "Unknown Product",
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer != null ? r.Reviewer.Username : "Unknown User",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                FlaggedBySystem = r.FlaggedBySystem,
                FlagReason = r.FlagReason,
                ReportedBySeller = r.ReportedBySeller,
                SellerReportReason = r.SellerReportReason,
                Reports = r.Product != null ? _context.ReviewReports
                    .Where(rep => rep.ReviewId == r.Id)
                    .Select(rep => new ReviewReportDto
                    {
                        Id = rep.Id,
                        ReporterName = rep.Reporter != null ? rep.Reporter.Username : "Anonymous",
                        Reason = rep.Reason,
                        Description = rep.Description,
                        CreatedAt = rep.CreatedAt
                    }).ToList() : new List<ReviewReportDto>()
            });

        return await PaginatedList<ReviewModerationDto>.CreateAsync(
            query, 
            request.PageNumber, 
            request.PageSize, 
            cancellationToken);
    }
}
