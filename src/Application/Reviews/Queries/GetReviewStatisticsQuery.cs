using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Queries;

public record GetReviewStatisticsQuery : IRequest<ReviewStatisticsDto>;

public class GetReviewStatisticsQueryHandler : IRequestHandler<GetReviewStatisticsQuery, ReviewStatisticsDto>
{
    private readonly IApplicationDbContext _context;

    public GetReviewStatisticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReviewStatisticsDto> Handle(GetReviewStatisticsQuery request, CancellationToken cancellationToken)
    {
        var allReviews = await _context.Reviews.AsNoTracking().ToListAsync(cancellationToken);
        var allReports = await _context.ReviewReports.AsNoTracking().ToListAsync(cancellationToken);

        int totalReviews = allReviews.Count;
        int flaggedCount = allReviews.Count(r => r.FlaggedBySystem || r.Status == "PendingReview" || r.Status == "Hidden" || r.ReportedBySeller || allReports.Any(rep => rep.ReviewId == r.Id));
        int pendingCount = allReviews.Count(r => r.Status == "PendingReview" || (allReports.Any(rep => rep.ReviewId == r.Id && rep.Status == "Pending") && r.Status != "Hidden"));
        int hiddenCount = allReviews.Count(r => r.Status == "Hidden");
        int visibleCount = allReviews.Count(r => r.Status == "Visible");
        int sellerReportedCount = allReviews.Count(r => r.ReportedBySeller);
        int communityReportedCount = allReports.Select(rep => rep.ReviewId).Distinct().Count();

        decimal avgRating = totalReviews > 0 
            ? Math.Round((decimal)allReviews.Where(r => r.Rating.HasValue).Average(r => r.Rating!.Value), 2)
            : 0;

        var ratingDistribution = new Dictionary<int, int>
        {
            { 1, allReviews.Count(r => r.Rating == 1) },
            { 2, allReviews.Count(r => r.Rating == 2) },
            { 3, allReviews.Count(r => r.Rating == 3) },
            { 4, allReviews.Count(r => r.Rating == 4) },
            { 5, allReviews.Count(r => r.Rating == 5) }
        };

        return new ReviewStatisticsDto
        {
            TotalReviews = totalReviews,
            FlaggedCount = flaggedCount,
            PendingCount = pendingCount,
            HiddenCount = hiddenCount,
            VisibleCount = visibleCount,
            SellerReportedCount = sellerReportedCount,
            CommunityReportedCount = communityReportedCount,
            AverageRating = avgRating,
            RatingDistribution = ratingDistribution
        };
    }
}
