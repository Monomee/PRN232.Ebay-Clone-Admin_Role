using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Products.Queries.GetProducts;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Products;

public class GetProductsTests
{
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnOnlyActiveProducts()
    {
        // Arrange
        var seller = new User { Id = 1, Username = "Seller1" };
        _context.Users.Add(seller);

        _context.Products.AddRange(new List<Product>
        {
            new Product { Id = 1, Title = "P1", Status = "Active", SellerId = 1 },
            new Product { Id = 2, Title = "P2", Status = "Hidden", SellerId = 1 },
            new Product { Id = 3, Title = "P3", Status = "Active", SellerId = 1 }
        });
        await _context.SaveChangesAsync();

        var handler = new GetProductsQueryHandler(_context);
        var query = new GetProductsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result.Any(p => p.Id == 1).ShouldBeTrue();
        result.Any(p => p.Id == 2).ShouldBeFalse();
        result.Any(p => p.Id == 3).ShouldBeTrue();
        result.All(p => p.SellerName == "Seller1").ShouldBeTrue();
    }
}
