using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Mappings;
using EbayClone.Application.Common.Models;
using EbayClone.Application.Products.Queries.DTOs;

namespace EbayClone.Application.Products.Queries.GetSellerProducts;

// Positional with defaults: [AsParameters] only treats a query value as optional when the constructor gives it a default
// (property initializers are ignored), so a missing ?pageNumber= falls back here instead of failing the request.
public record GetSellerProductsQuery(
    int SellerId = 0,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<ProductDto>>;

public class GetSellerProductsQueryHandler : IRequestHandler<GetSellerProductsQuery, PaginatedList<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSellerProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ProductDto>> Handle(GetSellerProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Where(p => p.SellerId == request.SellerId)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking();

        return await query.Select(p => new ProductDto
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            Price = p.Price,
            Images = p.Images,
            Status = p.Status,
            SellerId = p.SellerId,
            SellerName = p.Seller != null ? p.Seller.Username : "Unknown"
        })
        .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken: cancellationToken);
    }
}
