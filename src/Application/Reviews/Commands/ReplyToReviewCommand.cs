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
    private readonly IUser? _user;

    public ReplyToReviewCommandHandler(IApplicationDbContext context, IUser? user = null)
    {
        _context = context;
        _user = user;
    }

    public async Task<bool> Handle(ReplyToReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);

        if (review == null)
            throw new Exception("Không tìm thấy đánh giá.");

        var sellerId = (_user != null && int.TryParse(_user.Id, out var currentUserId) && currentUserId > 0)
            ? currentUserId
            : request.SellerId;

        // Ensure the person replying is the seller of the product
        if (review.Product?.SellerId != sellerId)
            throw new EbayClone.Application.Common.Exceptions.ForbiddenAccessException();

        review.SellerReply = request.Reply;
        review.SellerReplyCreatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
