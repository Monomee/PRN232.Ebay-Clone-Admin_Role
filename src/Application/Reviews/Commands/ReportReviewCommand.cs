using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Commands;

public record ReportReviewCommand : IRequest<bool>
{
    public int ReviewId { get; init; }
    public int? ReporterUserId { get; init; }
    public string Reason { get; init; } = null!;
    public string? Description { get; init; }
}

public class ReportReviewCommandHandler : IRequestHandler<ReportReviewCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ReportReviewCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ReportReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);
 
        if (review == null)
            throw new Exception("Không tìm thấy đánh giá.");
 
        // If the reporter is the seller of the product, we maintain the legacy flags for quick access
        bool isSeller = request.ReporterUserId.HasValue && review.Product?.SellerId == request.ReporterUserId;
        
        if (isSeller)
        {
            review.ReportedBySeller = true;
            review.SellerReportReason = request.Reason;
        }

        // Create the official report entry
        var report = new Domain.Entities.ReviewReport
        {
            ReviewId = request.ReviewId,
            ReporterUserId = request.ReporterUserId,
            Reason = request.Reason,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        _context.ReviewReports.Add(report);
        
        // Ensure review is flagged for admin attention
        review.Status = "PendingReview";
 
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
