using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Products.Queries.DTOs;

namespace EbayClone.Application.Products.Queries.GetProducts;
public record GetProductsQuery : IRequest<List<ProductDto>>;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.Status == "Active")
            .Include(p => p.Seller)
            .Select(p => new ProductDto
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
            .ToListAsync(cancellationToken);
    }
}
