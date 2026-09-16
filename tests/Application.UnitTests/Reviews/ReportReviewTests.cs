using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Reviews.Commands;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Reviews;

public class ReportReviewTests
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
    public async Task Handle_ValidReport_ShouldCreateReportAndFlagReview()
    {
        // Arrange
        var product = new Product { Id = 101, Title = "Product 101", SellerId = 10 };
        _context.Products.Add(product);

        var review = new Review { Id = 1, ProductId = 101, Status = "Visible" };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var handler = new ReportReviewCommandHandler(_context);
        var command = new ReportReviewCommand 
        { 
            ReviewId = 1, 
            ReporterUserId = 20, 
            Reason = "Spam", 
            Description = "This is a spam review." 
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        var updatedReview = await _context.Reviews.FindAsync(1);
        updatedReview!.Status.ShouldBe("PendingReview");

        var report = await _context.ReviewReports.FirstOrDefaultAsync(r => r.ReviewId == 1);
        report.ShouldNotBeNull();
        report.Reason.ShouldBe("Spam");
        report.ReporterUserId.ShouldBe(20);
    }

    [Test]
    public async Task Handle_ReportBySeller_ShouldSetReportedBySellerFlag()
    {
        // Arrange
        var product = new Product { Id = 102, Title = "Product 102", SellerId = 10 };
        _context.Products.Add(product);

        var review = new Review { Id = 2, ProductId = 102, Status = "Visible" };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var handler = new ReportReviewCommandHandler(_context);
        var command = new ReportReviewCommand 
        { 
            ReviewId = 2, 
            ReporterUserId = 10, // Same as SellerId
            Reason = "Abusive", 
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedReview = await _context.Reviews.FindAsync(2);
        updatedReview!.ReportedBySeller.ShouldBeTrue();
        updatedReview.SellerReportReason.ShouldBe("Abusive");
    }
}
