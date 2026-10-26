using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Queries;

public record GetSellerFeedbacksQuery : IRequest<PaginatedList<SellerFeedbackDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public string? Tier { get; init; } // 'All', 'TopRated', 'AboveStandard', 'BelowStandard'
}

public class GetSellerFeedbacksQueryHandler : IRequestHandler<GetSellerFeedbacksQuery, PaginatedList<SellerFeedbackDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSellerFeedbacksQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<SellerFeedbackDto>> Handle(GetSellerFeedbacksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Feedbacks
            .AsNoTracking()
            .Include(f => f.Seller)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(f => 
                (f.Seller != null && f.Seller.Username != null && f.Seller.Username.ToLower().Contains(term)) ||
                (f.Seller != null && f.Seller.Email != null && f.Seller.Email.ToLower().Contains(term))
            );
        }

        var projected = query
            .OrderByDescending(f => f.AverageRating)
            .Select(f => new SellerFeedbackDto
            {
                Id = f.Id,
                SellerId = f.SellerId,
                SellerName = f.Seller != null ? f.Seller.Username : "Unknown Seller",
                SellerEmail = f.Seller != null ? f.Seller.Email : "N/A",
                AverageRating = f.AverageRating.HasValue ? Math.Round(f.AverageRating.Value, 2) : 0,
                TotalReviews = f.TotalReviews ?? 0,
                PositiveRate = f.PositiveRate.HasValue ? Math.Round(f.PositiveRate.Value, 1) : 0,
                SellerStatus = f.Seller != null ? f.Seller.Status : "Active",
                ReputationTier = (f.PositiveRate >= 95 && f.AverageRating >= 4.5m) ? "TopRated" :
                                 (f.PositiveRate >= 80 && f.AverageRating >= 3.5m) ? "AboveStandard" : "BelowStandard"
            });

        if (!string.IsNullOrWhiteSpace(request.Tier) && !request.Tier.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            projected = projected.Where(s => s.ReputationTier == request.Tier);
        }

        return await PaginatedList<SellerFeedbackDto>.CreateAsync(
            projected, 
            request.PageNumber, 
            request.PageSize, 
            cancellationToken);
    }
}
