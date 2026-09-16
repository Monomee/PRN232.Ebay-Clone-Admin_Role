using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Orders.Queries.GetOrderDetail;

public record OrderDetailDto
{
    public int Id { get; init; }
    public DateTime? OrderDate { get; init; }
    public decimal? TotalPrice { get; init; }
    public string? Status { get; init; }
    public string? BuyerName { get; init; }
    public string? BuyerEmail { get; init; }
    public string? ShippingAddress { get; init; }
    public List<OrderItemDto> Items { get; init; } = new();
}

public record OrderItemDto
{
    public int Id { get; init; }
    public string? ProductName { get; init; }
    public decimal? UnitPrice { get; init; }
    public int? Quantity { get; init; }
    public string? SellerName { get; init; }
}

public record GetOrderDetailQuery(int Id) : IRequest<OrderDetailDto?>;

public class GetOrderDetailQueryHandler : IRequestHandler<GetOrderDetailQuery, OrderDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetOrderDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDetailDto?> Handle(GetOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.OrderTables
            .Include(o => o.Buyer)
            .Include(o => o.Address)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.Seller)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (order == null) return null;

        return new OrderDetailDto
        {
            Id = order.Id,
            OrderDate = order.OrderDate,
            TotalPrice = order.TotalPrice,
            Status = order.Status,
            BuyerName = order.Buyer?.Username ?? "Unknown",
            BuyerEmail = order.Buyer?.Email,
            ShippingAddress = order.Address != null 
                ? $"{order.Address.Street}, {order.Address.City}, {order.Address.State}, {order.Address.Country}"
                : "No address provided",
            Items = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ProductName = oi.Product?.Title ?? "Unknown Product",
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                SellerName = oi.Product?.Seller?.Username ?? "Unknown Seller"
            }).ToList()
        };
    }
}
