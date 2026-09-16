using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;

namespace EbayClone.Application.ReturRequests.Queries.GetReturnRequests;
public record ReturnRequestListItemDto
{
    public int Id { get; init; }
    public int? OrderId { get; init; }
    public string? BuyerUsername { get; init; }
    public string? BuyerEmail { get; init; }
    public string? Reason { get; init; }
    public string? Status { get; init; }
    public decimal? TotalPrice { get; init; }
    public DateTime? CreatedAt { get; init; }
}

// Query với filter theo status
public record GetReturnRequestsQuery(string? Status = "Pending")
    : IRequest<List<ReturnRequestListItemDto>>;

public class GetReturnRequestsQueryHandler
    : IRequestHandler<GetReturnRequestsQuery, List<ReturnRequestListItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReturnRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReturnRequestListItemDto>> Handle(
        GetReturnRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ReturnRequests
            .Include(r => r.User)
            .Include(r => r.Order)
            .AsQueryable();

        // Lọc theo status (mặc định "Pending" cho tab chờ xử lý)
        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(r => r.Status == request.Status);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReturnRequestListItemDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                BuyerUsername = r.User != null ? r.User.Username : null,
                BuyerEmail = r.User != null ? r.User.Email : null,
                Reason = r.Reason,
                Status = r.Status,
                TotalPrice = r.Order != null ? r.Order.TotalPrice : null,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
