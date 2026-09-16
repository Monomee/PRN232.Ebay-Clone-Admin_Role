using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Mappings;
using EbayClone.Application.Common.Models;
using EbayClone.Application.Products.Queries.DTOs;

namespace EbayClone.Application.Products.Queries.GetSellerProducts;

public record GetSellerProductsQuery : IRequest<PaginatedList<ProductDto>>
{
    public int SellerId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

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
