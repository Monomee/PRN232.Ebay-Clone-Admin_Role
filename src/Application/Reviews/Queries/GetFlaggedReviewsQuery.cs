using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Application.Reviews;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Queries;

public record GetFlaggedReviewsQuery : IRequest<PaginatedList<ReviewModerationDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Status { get; init; } // 'All', 'PendingReview', 'Hidden', 'Visible'
    public string? Reason { get; init; } // 'All', 'SellerReported', 'CommunityReported', 'FlaggedBySystem', 'LowRating'
    public string? SearchTerm { get; init; }
    public int? Rating { get; init; }
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
            .Include(r => r.Product)
                .ThenInclude(p => p!.Seller)
            .Include(r => r.Reviewer)
            .AsQueryable();

        // 1. Lọc theo trạng thái cắm cờ / kiểm duyệt cơ bản
        // Bao gồm: Đã bị cắm cờ hệ thống, hoặc đang chờ duyệt, hoặc đã ẩn, hoặc Seller báo cáo, hoặc có Report từ người dùng
        query = query.Where(r => r.FlaggedBySystem 
                              || r.Status == "PendingReview" 
                              || r.Status == "Hidden" 
                              || r.ReportedBySeller 
                              || _context.ReviewReports.Any(rep => rep.ReviewId == r.Id));

        // 2. Lọc theo Status cụ thể nếu có yêu cầu
        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.Status == request.Status);
        }

        // 3. Lọc theo Reason (Lý do vi phạm)
        if (!string.IsNullOrWhiteSpace(request.Reason) && !request.Reason.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            switch (request.Reason.ToLower())
            {
                case "sellerreported":
                    query = query.Where(r => r.ReportedBySeller);
                    break;
                case "communityreported":
                    query = query.Where(r => _context.ReviewReports.Any(rep => rep.ReviewId == r.Id));
                    break;
                case "flaggedbysystem":
                    query = query.Where(r => r.FlaggedBySystem);
                    break;
                case "lowrating":
                    query = query.Where(r => r.Rating == 1 || r.Rating == 2);
                    break;
            }
        }

        // 4. Lọc theo Rating
        if (request.Rating.HasValue && request.Rating.Value > 0)
        {
            query = query.Where(r => r.Rating == request.Rating.Value);
        }

        // 5. Tìm kiếm từ khóa (Reviewer, Product Title, Seller, Comment)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(r => 
                (r.Comment != null && r.Comment.ToLower().Contains(term)) ||
                (r.Reviewer != null && r.Reviewer.Username != null && r.Reviewer.Username.ToLower().Contains(term)) ||
                (r.Product != null && r.Product.Title != null && r.Product.Title.ToLower().Contains(term)) ||
                (r.Product != null && r.Product.Seller != null && r.Product.Seller.Username != null && r.Product.Seller.Username.ToLower().Contains(term))
            );
        }

        var projectedQuery = query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewModerationDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductTitle = r.Product != null ? r.Product.Title : "Unknown Product",
                SellerId = r.Product != null ? r.Product.SellerId : null,
                SellerName = r.Product != null && r.Product.Seller != null ? r.Product.Seller.Username : "N/A",
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer != null ? r.Reviewer.Username : "Anonymous",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                FlaggedBySystem = r.FlaggedBySystem,
                FlagReason = r.FlagReason,
                ReportedBySeller = r.ReportedBySeller,
                SellerReportReason = r.SellerReportReason,
                SellerReply = r.SellerReply,
                SellerReplyCreatedAt = r.SellerReplyCreatedAt,
                ModeratedBy = r.ModeratedBy,
                ModeratedAt = r.ModeratedAt,
                ModerationAction = r.ModerationAction,
                Reports = _context.ReviewReports
                    .Where(rep => rep.ReviewId == r.Id)
                    .Select(rep => new ReviewReportDto
                    {
                        Id = rep.Id,
                        ReporterName = rep.Reporter != null ? rep.Reporter.Username : "Anonymous",
                        Reason = rep.Reason,
                        Description = rep.Description,
                        CreatedAt = rep.CreatedAt,
                        Status = rep.Status
                    }).ToList()
            });

        return await PaginatedList<ReviewModerationDto>.CreateAsync(
            projectedQuery, 
            request.PageNumber, 
            request.PageSize, 
            cancellationToken);
    }
}
