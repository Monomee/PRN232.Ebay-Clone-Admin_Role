using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Mappings;
using EbayClone.Application.Common.Models; // Giả sử có PaginatedList
using EbayClone.Application.Products.Queries.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record GetManagedProductsQuery : IRequest<PaginatedList<ManagedProductDto>>
{
    public string Tab { get; set; } = "All"; // "All" hoặc "Reported"
    public int? CategoryId { get; set; }
    public string? ShopName { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class GetManagedProductsQueryHandler : IRequestHandler<GetManagedProductsQuery, PaginatedList<ManagedProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetManagedProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ManagedProductDto>> Handle(GetManagedProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Reports) // Load reports để check VeRO
            .AsNoTracking();

        // 1. Logic Tab "Đã báo cáo"
        if (request.Tab == "Reported")
        {
            query = query.Where(p => p.Reports.Any(r => r.Status == "Pending"));
        }

        // 2. Logic Lọc
        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId);

        if (!string.IsNullOrEmpty(request.ShopName))
            query = query.Where(p => p.Seller != null &&
                                   ((p.Seller.Username != null && p.Seller.Username.Contains(request.ShopName)) ||
                                    (p.Seller.Email != null && p.Seller.Email.Contains(request.ShopName))));

        // 3. SẮP XẾP: VeRO lên đầu -> Nhiều report -> Mới nhất
        if (request.Tab == "Reported")
        {
            query = query.OrderByDescending(p => p.Reports.Any(r => r.ReporterType == "VeRO" && r.Status == "Pending")) // VeRO ưu tiên số 1
                         .ThenByDescending(p => p.ReportCount)
                         .ThenByDescending(p => p.CreatedAt); // Created từ BaseAuditableEntity
        }
        else
        {
            query = query.OrderByDescending(p => p.CreatedAt);
        }

        // 4. Map sang DTO
        return await query.Select(p => new ManagedProductDto
        {
            Id = p.Id,
            Title = p.Title,
            Price = p.Price ?? 0,
            ImageUrl = p.Images,
            CategoryName = p.Category != null ? p.Category.Name : null, 
            ShopName = p.Seller != null ? p.Seller.Username : null,
            ReportReasons = p.Reports
                         .Where(r => r.Status == "Pending" && r.Reason != null)
                         .Select(r => r.Reason!)
                         .ToList(),
            Status = p.Status,
            ReportCount = p.ReportCount,
            IsVeroViolation = p.Reports.Any(r => r.ReporterType == "VeRO" && r.Status == "Pending"),
            Priority = p.Reports.Any(r => r.ReporterType == "VeRO") ? "Critical" : "Medium"
        })
        .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken: cancellationToken);
    }
}
