using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Products.Queries.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Products.Queries.GetProductById;

public record GetProductByIdQuery(int Id) : IRequest<ProductDetailDto?>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Seller)
            .Include(p => p.Reviews.Where(r => r.Status == "Visible")) // Only include visible reviews
            .ThenInclude(r => r.Reviewer)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
            return null;

        return new ProductDetailDto
        {
            Id = product.Id,
            Title = product.Title,
            Description = product.Description,
            Price = product.Price,
            Images = product.Images,
            Status = product.Status,
            SellerId = product.SellerId,
            SellerName = product.Seller?.Username ?? "Unknown",
            IsAuction = product.IsAuction,
            AuctionEndTime = product.AuctionEndTime,
            Reviews = product.Reviews.Select(r => new ReviewDto
            {
                Id = r.Id,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer?.Username ?? "Anonymous",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                SellerReply = r.SellerReply,
                SellerReplyCreatedAt = r.SellerReplyCreatedAt
            }).ToList()
        };
    }
}
