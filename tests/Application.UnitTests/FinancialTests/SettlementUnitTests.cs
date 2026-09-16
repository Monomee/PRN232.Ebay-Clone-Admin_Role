using EbayClone.Application.Financials.Commands.SettleOrder;
using EbayClone.Application.Financials.Commands.SettlePendingFunds;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using NUnit.Framework;
using Moq;
using EbayClone.Application.Common.Interfaces;

namespace EbayClone.Application.UnitTests.FinancialTests;

public class SettlementUnitTests
{
    private ApplicationDbContext _context = null!;
    private Mock<ISellerHubService> _sellerHubServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sellerHubServiceMock = new Mock<ISellerHubService>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_SettlePendingFunds_ShouldSettleOnlyEligibleOrders()
    {
        // Arrange
        var seller = new User { Id = 3, Username = "Seller3" };
        _context.Users.Add(seller);

        var wallet = new SellerWallet { Id = 3, SellerId = 3, PendingBalance = 1000, AvailableBalance = 0 };
        _context.SellerWallets.Add(wallet);

        // Eligible
        var order1 = new OrderTable 
        { 
            Id = 101, 
            Status = "Delivered", 
            CompletedAt = DateTime.UtcNow.AddDays(-10), 
            CanDisputeUntil = DateTime.UtcNow.AddDays(-3),
            SellerEarnings = 500 
        };
        order1.OrderItems.Add(new OrderItem { Product = new Product { SellerId = 3 } });
        _context.OrderTables.Add(order1);

        // Not eligible (in dispute)
        var order2 = new OrderTable 
        { 
            Id = 102, 
            Status = "Delivered", 
            CompletedAt = DateTime.UtcNow.AddDays(-1), 
            CanDisputeUntil = DateTime.UtcNow.AddDays(6),
            SellerEarnings = 300 
        };
        order2.OrderItems.Add(new OrderItem { Product = new Product { SellerId = 3 } });
        _context.OrderTables.Add(order2);

        await _context.SaveChangesAsync();

        var handler = new SettlePendingFundsCommandHandler(_context, _sellerHubServiceMock.Object);

        // Act
        var result = await handler.Handle(new SettlePendingFundsCommand(), CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        
        var updatedOrder1 = await _context.OrderTables.FindAsync(101);
        updatedOrder1!.Status.ShouldBe("FundsCleared");

        var updatedOrder2 = await _context.OrderTables.FindAsync(102);
        updatedOrder2!.Status.ShouldBe("Delivered");

        var updatedWallet = await _context.SellerWallets.FindAsync(3);
        updatedWallet!.PendingBalance.ShouldBe(500);
        updatedWallet.AvailableBalance.ShouldBe(500);
    }

    [Test]
    public async Task Handle_SettleOrder_ShouldSettleSpecificOrder()
    {
        // Arrange
        var seller = new User { Id = 4, Username = "Seller4" };
        _context.Users.Add(seller);

        var wallet = new SellerWallet { Id = 4, SellerId = 4, PendingBalance = 500, AvailableBalance = 0 };
        _context.SellerWallets.Add(wallet);

        var order = new OrderTable 
        { 
            Id = 103, 
            Status = "Delivered", 
            CompletedAt = DateTime.UtcNow.AddDays(-10), 
            CanDisputeUntil = DateTime.UtcNow.AddDays(-3),
            SellerEarnings = 500 
        };
        order.OrderItems.Add(new OrderItem { Product = new Product { SellerId = 4 } });
        _context.OrderTables.Add(order);
        await _context.SaveChangesAsync();

        var handler = new SettleOrderCommandHandler(_context);

        // Act
        var result = await handler.Handle(new SettleOrderCommand(103), CancellationToken.None);

        // Assert
        result.ShouldBeTrue();

        var updatedOrder = await _context.OrderTables.FindAsync(103);
        updatedOrder!.Status.ShouldBe("FundsCleared");

        var updatedWallet = await _context.SellerWallets.FindAsync(4);
        updatedWallet!.PendingBalance.ShouldBe(0);
        updatedWallet.AvailableBalance.ShouldBe(500);

        var transaction = await _context.FinancialTransactions.FirstOrDefaultAsync(t => t.OrderId == 103);
        transaction.ShouldNotBeNull();
        transaction!.Type.ShouldBe("Settlement");
    }
}
