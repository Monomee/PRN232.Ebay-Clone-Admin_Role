using System.Linq;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Reviews.Queries;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Reviews;

public class GetFlaggedReviewsTests
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
    public async Task Handle_ShouldReturnOnlyFlaggedOrPendingOrHiddenReviews()
    {
        // Arrange
        var product = new Product { Id = 1, Title = "Product 1" };
        _context.Products.Add(product);

        var user = new User { Id = 1, Username = "User 1" };
        _context.Users.Add(user);

        _context.Reviews.AddRange(new List<Review>
        {
            new Review { Id = 1, ProductId = 1, ReviewerId = 1, Status = "Visible", FlaggedBySystem = false, CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Review { Id = 2, ProductId = 1, ReviewerId = 1, Status = "PendingReview", FlaggedBySystem = true, CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new Review { Id = 3, ProductId = 1, ReviewerId = 1, Status = "Hidden", FlaggedBySystem = false, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Review { Id = 4, ProductId = 1, ReviewerId = 1, Status = "Visible", FlaggedBySystem = true, CreatedAt = DateTime.UtcNow }
        });

        await _context.SaveChangesAsync();

        var handler = new GetFlaggedReviewsQueryHandler(_context);
        var query = new GetFlaggedReviewsQuery { PageNumber = 1, PageSize = 10 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.Any(r => r.Id == 1).ShouldBeFalse();
        result.Items.Any(r => r.Id == 2).ShouldBeTrue();
        result.Items.Any(r => r.Id == 3).ShouldBeTrue();
        result.Items.Any(r => r.Id == 4).ShouldBeTrue();
        
        // Order by CreatedAt Desc
        result.Items.First().Id.ShouldBe(4);
    }
}
