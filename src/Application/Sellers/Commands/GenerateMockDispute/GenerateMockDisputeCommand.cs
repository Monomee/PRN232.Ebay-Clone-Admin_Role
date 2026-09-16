using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EbayClone.Application.Sellers.Commands.GenerateMockDispute;

public record GenerateMockDisputeCommand : IRequest<bool>
{
    public int SellerId { get; init; }
}

public class GenerateMockDisputeCommandHandler : IRequestHandler<GenerateMockDisputeCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public GenerateMockDisputeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(GenerateMockDisputeCommand request, CancellationToken cancellationToken)
    {
        var seller = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.SellerId && u.Role == "Seller", cancellationToken);
        if (seller == null) throw new ArgumentException($"Seller {request.SellerId} not found.");

        var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Buyer", cancellationToken);
        if (buyer == null) throw new ArgumentException("No buyer found in DB to mock order.");

        var orderDate = DateTime.UtcNow.AddDays(-5);
        
        // 1. Create a mock order
        var order = new OrderTable
        {
            BuyerId = buyer.Id,
            OrderDate = orderDate,
            TotalPrice = 150.00m,
            Status = "InDispute",
            CompletedAt = null,
            CanDisputeUntil = orderDate.AddDays(14),
            PlatformFee = 15.00m,
            SellerEarnings = 135.00m
        };

        _context.OrderTables.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Add an order item linking to the seller
        var product = await _context.Products.FirstOrDefaultAsync(p => p.SellerId == seller.Id, cancellationToken);
        if (product == null)
        {
            product = new Product { SellerId = seller.Id, Title = "Mock Dispute Product", Price = 150.00m, Description = "Mock" };
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 150.00m
        };
        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync(cancellationToken);

        // 3. Create the Dispute
        var evidenceList = new[] 
        {
            new { type = "image", url = "https://picsum.photos/seed/mock_dispute/400/300", description = "Mock evidence from buyer" }
        };

        var dispute = new Dispute
        {
            CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-MOCK",
            OrderId = order.Id,
            RaisedBy = buyer.Id,
            Type = "ItemNotAsDescribed",
            Subcategory = "DamagedItem",
            Description = "The item arrived completely smashed. I generated this via the developer mock toolbox.",
            DesiredOutcome = "FullRefund",
            Amount = 150.00m,
            Priority = "High",
            Status = "Open",
            CreatedAt = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(3),
            BuyerEvidence = JsonSerializer.Serialize(evidenceList),
            RequiresReturn = false,
            ViewCount = 0
        };

        _context.Disputes.Add(dispute);
        
        // 4. Freeze funds in Seller Wallet
        var wallet = await _context.SellerWallets.FirstOrDefaultAsync(w => w.SellerId == seller.Id, cancellationToken);
        if (wallet != null)
        {
            if (wallet.PendingBalance >= 135.00m)
            {
                wallet.PendingBalance -= 135.00m;
                wallet.DisputedBalance += 135.00m;
            }
        }
        
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
