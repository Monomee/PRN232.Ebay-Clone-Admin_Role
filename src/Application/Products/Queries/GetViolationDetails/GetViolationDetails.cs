using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Products.Queries.DTOs;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Products.Queries.GetViolationDetails;
public record GetViolationDetailsQuery(int ProductId) : IRequest<ViolationDetailDto>;

// 3. Query Handler
public class GetViolationDetailsQueryHandler : IRequestHandler<GetViolationDetailsQuery, ViolationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetViolationDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ViolationDetailDto> Handle(GetViolationDetailsQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Seller)
            .Include(p => p.Reports.Where(r => r.Status == "Pending")) // Chỉ lấy các báo cáo đang chờ xử lý
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
            throw new NotFoundException(nameof(Product), $"{request.ProductId}");

        return new ViolationDetailDto
        {
            ProductId = product.Id,
            Title = product.Title,
            Description = product.Description,
            ShopEmail = product.Seller?.Email,
            Reports = product.Reports.Select(r => new ReportItemDto
            {
                ReportId = r.Id,
                ReporterType = r.ReporterType,
                Reason = r.Reason,
                ProofDocumentUrl = r.EvidenceFiles,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
