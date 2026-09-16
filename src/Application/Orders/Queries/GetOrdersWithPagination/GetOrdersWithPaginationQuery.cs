using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Orders.Queries.GetOrdersWithPagination;

public record OrderBriefDto
{
    public int Id { get; init; }
    public DateTime? OrderDate { get; init; }
    public decimal? TotalPrice { get; init; }
    public string? Status { get; init; }
    public string? BuyerName { get; init; }
    public int ItemCount { get; init; }
}

public record GetOrdersWithPaginationQuery : IRequest<PaginatedList<OrderBriefDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Status { get; init; }
    public string? Search { get; init; }
}

public class GetOrdersWithPaginationQueryHandler : IRequestHandler<GetOrdersWithPaginationQuery, PaginatedList<OrderBriefDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrdersWithPaginationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<OrderBriefDto>> Handle(GetOrdersWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.OrderTables
            .Include(o => o.Buyer)
            .Include(o => o.OrderItems)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Status) && request.Status != "All")
        {
            query = query.Where(o => o.Status == request.Status);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(o => 
                o.Id.ToString().Contains(search) || 
                (o.Buyer != null && o.Buyer.Username!.ToLower().Contains(search)));
        }

        query = query.OrderByDescending(o => o.OrderDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrderBriefDto
            {
                Id = o.Id,
                OrderDate = o.OrderDate,
                TotalPrice = o.TotalPrice,
                Status = o.Status,
                BuyerName = o.Buyer != null ? o.Buyer.Username : "Unknown",
                ItemCount = o.OrderItems.Count
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<OrderBriefDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
