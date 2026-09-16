using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Commands;

public record ReplyToReviewCommand : IRequest<bool>
{
    public int ReviewId { get; init; }
    public int SellerId { get; init; }
    public string Reply { get; init; } = null!;
}

public class ReplyToReviewCommandHandler : IRequestHandler<ReplyToReviewCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ReplyToReviewCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ReplyToReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);

        if (review == null)
            throw new Exception("Không tìm thấy đánh giá.");

        // Ensure the person replying is the seller of the product
        if (review.Product?.SellerId != request.SellerId)
            throw new Exception("Chỉ người bán sản phẩm này mới có quyền phản hồi.");

        review.SellerReply = request.Reply;
        review.SellerReplyCreatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
