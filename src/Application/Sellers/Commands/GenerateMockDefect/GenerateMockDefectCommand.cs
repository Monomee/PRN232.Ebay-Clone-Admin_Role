using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Commands.GenerateMockDefect;

public record GenerateMockDefectCommand : IRequest<bool>
{
    public int SellerId { get; init; }
}

public class GenerateMockDefectCommandHandler : IRequestHandler<GenerateMockDefectCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public GenerateMockDefectCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(GenerateMockDefectCommand request, CancellationToken cancellationToken)
    {
        var seller = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.SellerId && u.Role == "Seller", cancellationToken);
        if (seller == null) throw new ArgumentException($"Seller {request.SellerId} not found.");

        var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Buyer", cancellationToken);
        if (buyer == null) throw new ArgumentException("No buyer found in DB to mock order.");

        // Create a mock order that is 30 days old
        var orderDate = DateTime.UtcNow.AddDays(-30);
        var order = new OrderTable
        {
            BuyerId = buyer.Id,
            OrderDate = orderDate,
            TotalPrice = 500000, // 500k VND
            Status = "Returned", // The defect is that this order was refunded
            CompletedAt = null,
            CanDisputeUntil = orderDate.AddDays(14),
            PlatformFee = 25000,
            SellerEarnings = 475000
        };

        _context.OrderTables.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        // Add an order item linking to the seller
        var product = await _context.Products.FirstOrDefaultAsync(p => p.SellerId == seller.Id, cancellationToken);
        if (product == null)
        {
            // Create a dummy product if none exists
            product = new Product { SellerId = seller.Id, Title = "Mock Defect Product", Price = 500000, Description = "Mock" };
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 500000
        };
        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync(cancellationToken);

        // Add a Refund Return Request which counts as a defect (Status = Refunded)
        var returnReq = new ReturnRequest
        {
            OrderId = order.Id,
            CreatedAt = orderDate.AddDays(2),
            ResolvedAt = orderDate.AddDays(5),
            Reason = "Item defective",
            Status = "Refunded", // This triggers DefectRate calculation
            AdminNote = "Mock Full Refund",
            ResolutionAction = "RefundWithoutReturn"
        };
        _context.ReturnRequests.Add(returnReq);

        // Add a Dispute closed without resolution
        var dispute = new Dispute
        {
            OrderId = order.Id,
            CreatedAt = orderDate.AddDays(3),
            ResolvedAt = orderDate.AddDays(6),
            Description = "Seller unresponsive",
            Status = "ClosedWithoutResolution", // This triggers UnresolvedCases calculation
            AdminNotes = "Platform ruled in favor of buyer",
            Winner = "Buyer"
        };
        _context.Disputes.Add(dispute);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
