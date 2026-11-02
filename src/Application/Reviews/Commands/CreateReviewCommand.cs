using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using FluentValidation.Results;
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
    private readonly IUser? _user;

    public CreateReviewCommandHandler(IApplicationDbContext context, IServiceProvider serviceProvider, IUser? user = null)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _user = user;
    }

    public async Task<int> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.Rating), "Điểm đánh giá (Rating) phải nằm trong khoảng từ 1 đến 5.")]);
        }

        int reviewerId = request.ReviewerId;
        if (_user != null && int.TryParse(_user.Id, out var currentUserId) && currentUserId > 0)
        {
            reviewerId = currentUserId;
        }
        else if (reviewerId <= 0)
        {
            throw new UnauthorizedAccessException();
        }

        var reviewer = await _context.Users.FindAsync(new object[] { reviewerId }, cancellationToken);
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

        var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
        if (product != null)
        {
            if (product.SellerId == reviewerId)
            {
                throw new ValidationException([new ValidationFailure(nameof(request.ReviewerId), "Người bán không thể tự đánh giá sản phẩm của mình.")]);
            }

            // Verify the user actually purchased the product and order is completed/delivered
            var hasPurchased = await _context.OrderTables
                .Include(o => o.OrderItems)
                .AnyAsync(o => o.BuyerId == reviewerId 
                               && (o.Status == "Completed" || o.Status == "Delivered" || o.Status == "FundsCleared")
                               && o.OrderItems.Any(i => i.ProductId == request.ProductId), 
                    cancellationToken);

            if (!hasPurchased)
            {
                throw new ValidationException([new ValidationFailure(nameof(request.ProductId), "Bạn chỉ có thể đánh giá những sản phẩm đã mua thành công.")]);
            }
        }

        var review = new Review
        {
            ProductId = request.ProductId,
            ReviewerId = reviewerId,
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
