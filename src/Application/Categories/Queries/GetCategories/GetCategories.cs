using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Categories.Queries.DTOs;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Application.Products.Queries.DTOs;

namespace EbayClone.Application.Categories.Queries.GetCategories;
public record GetCategoriessQuery : IRequest<List<CategoryDto>>;

public class GetCategoriessQueryHandler : IRequestHandler<GetCategoriessQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoriessQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> Handle(GetCategoriessQuery request, CancellationToken cancellationToken)
    {
        return await _context.Categories
             .AsNoTracking()
             .Select(c => new CategoryDto
             {
                 Id = c.Id,
                 Name = c.Name
             })
             .ToListAsync(cancellationToken);
    }
}
