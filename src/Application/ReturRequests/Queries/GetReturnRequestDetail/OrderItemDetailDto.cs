using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.ReturRequests.Queries.GetReturnRequestDetail;
public record OrderItemDetailDto
{
    public int? ProductId { get; init; }
    public string? ProductTitle { get; init; }
    public int? Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
}

public record ReturnRequestDetailDto
{
    public int Id { get; init; }
    public int? OrderId { get; init; }
    public string? BuyerUsername { get; init; }
    public string? BuyerEmail { get; init; }
    public string? Reason { get; init; }
    public string? Status { get; init; }
    public string? EvidenceImages { get; init; }   // JSON string URLs ảnh bằng chứng
    public string? ShopSolution { get; init; }      // Giải pháp shop đề xuất
    public string? AdminNote { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }

    // Thông tin đơn hàng
    public decimal? TotalPrice { get; init; }
    public DateTime? OrderDate { get; init; }
    public string? OrderStatus { get; init; }
    public List<OrderItemDetailDto> OrderItems { get; init; } = new();

    // Ebay Dispute Tracking
    public bool IsRefundedByEbayFund { get; init; }
    public string? ResolutionAction { get; init; }
    public string? ReturnLabelUrl { get; init; }
    
    // Delivery & Tracking
    public string? TrackingNumber { get; init; }
    public string? DeliveryStatus { get; init; }

    // History / Timeline
    public List<AdminActionDto> History { get; init; } = new();
}

public record AdminActionDto
{
    public string? Action { get; init; }
    public string? Details { get; init; }
    public string? AdminUsername { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record GetReturnRequestDetailQuery(int Id)
    : IRequest<ReturnRequestDetailDto>;

public class GetReturnRequestDetailQueryHandler
    : IRequestHandler<GetReturnRequestDetailQuery, ReturnRequestDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetReturnRequestDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReturnRequestDetailDto> Handle(
        GetReturnRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        var returnRequest = await _context.ReturnRequests
            .Include(r => r.User)
            .Include(r => r.Order)
                .ThenInclude(o => o!.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .Include(r => r.Order)
                .ThenInclude(o => o!.ShippingInfos)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (returnRequest == null)
            throw new NotFoundException(nameof(ReturnRequest), $"{request.Id}");

        // Fetch History (Audit Log)
        var history = await _context.AdminActions
            .Include(a => a.Admin)
            .Where(a => a.TargetType == "ReturnRequest" && a.TargetId == returnRequest.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdminActionDto
            {
                Action = a.Action,
                Details = a.Details,
                AdminUsername = a.Admin != null ? a.Admin.Username : "System",
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new ReturnRequestDetailDto
        {
            Id = returnRequest.Id,
            OrderId = returnRequest.OrderId,
            BuyerUsername = returnRequest.User?.Username,
            BuyerEmail = returnRequest.User?.Email,
            Reason = returnRequest.Reason,
            Status = returnRequest.Status,
            EvidenceImages = returnRequest.EvidenceImages,
            ShopSolution = returnRequest.ShopSolution,
            AdminNote = returnRequest.AdminNote,
            CreatedAt = returnRequest.CreatedAt,
            ResolvedAt = returnRequest.ResolvedAt,
            TotalPrice = returnRequest.Order?.TotalPrice,
            OrderDate = returnRequest.Order?.OrderDate,
            OrderStatus = returnRequest.Order?.Status,
            IsRefundedByEbayFund = returnRequest.IsRefundedByEbayFund,
            ResolutionAction = returnRequest.ResolutionAction,
            ReturnLabelUrl = returnRequest.ReturnLabelUrl,
            TrackingNumber = returnRequest.Order?.ShippingInfos.FirstOrDefault()?.TrackingNumber,
            DeliveryStatus = returnRequest.Order?.ShippingInfos.FirstOrDefault()?.Status,
            History = history,
            OrderItems = returnRequest.Order?.OrderItems
                .Select(oi => new OrderItemDetailDto
                {
                    ProductId = oi.ProductId,
                    ProductTitle = oi.Product?.Title,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice
                }).ToList() ?? new List<OrderItemDetailDto>()
        };
    }
}
