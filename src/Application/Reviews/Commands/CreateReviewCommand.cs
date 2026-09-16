using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Commands;

public record CreateReviewCommand : IRequest<int>
{
    public int ProductId { get; init; }
    public int ReviewerId { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
}

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;

    public CreateReviewCommandHandler(IApplicationDbContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public async Task<int> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var reviewer = await _context.Users.FindAsync(new object[] { request.ReviewerId }, cancellationToken);
        if (reviewer == null)
            throw new Exception("Không tìm thấy người dùng.");

        if (reviewer != null)
        {
            if (reviewer.Status == "Banned")
                throw new Exception("Tài khoản của bạn đã bị khóa.");
            if (reviewer.IsReviewRestricted)
                throw new Exception("Bạn đã bị hạn chế chức năng đánh giá vĩnh viễn.");
            if (reviewer.ReviewBanUntil.HasValue && reviewer.ReviewBanUntil.Value > DateTime.UtcNow)
                throw new Exception($"Chức năng đánh giá của bạn bị tạm khóa đến {reviewer.ReviewBanUntil.Value:dd/MM/yyyy HH:mm}.");
        }

        // Verify the user actually purchased the product and order is completed
        //var hasPurchased = await _context.OrderTables
        //    .Include(o => o.OrderItems)
        //    .AnyAsync(o => o.BuyerId == request.ReviewerId 
        //                   && o.Status == "Completed" 
        //                   && o.OrderItems.Any(i => i.ProductId == request.ProductId), 
        //        cancellationToken);

        //if (!hasPurchased)
        //{
        //    throw new Exception("Bạn chỉ có thể đánh giá những sản phẩm đã mua thành công.");
        //}

        var review = new Review
        {
            ProductId = request.ProductId,
            ReviewerId = request.ReviewerId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow,
            Status = "Visible",
            FlaggedBySystem = false
        };

        // 1. Logic gắn cờ dựa trên Rating
        if (request.Rating == 1)
        {
            review.FlaggedBySystem = true;
            review.FlagReason = "LowRating";
            review.Status = "PendingReview";
        }

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Logic gắn cờ dựa trên AI Content Moderation chạy ngầm (Background)
        if (!string.IsNullOrEmpty(request.Comment))
        {
            var reviewId = review.Id;
            var comment = request.Comment;

            _ = Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var moderationService = scope.ServiceProvider.GetRequiredService<IContentModerationService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                try
                {
                    var (isFlagged, reason) = await moderationService.ModerateContentAsync(comment, default);
                    
                    if (isFlagged)
                    {
                        var r = await dbContext.Reviews.FindAsync(new object[] { reviewId });
                        if (r != null)
                        {
                            r.FlaggedBySystem = true;
                            r.FlagReason = string.IsNullOrEmpty(r.FlagReason) ? reason : r.FlagReason + ", " + reason;
                            r.Status = "PendingReview";
                            await dbContext.SaveChangesAsync(default);
                        }
                    }
                }
                catch
                {
                    // Fail silently in background
                }
            });
        }

        return review.Id;
    }
}
