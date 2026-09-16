using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Reviews.Commands;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Reviews;

public class CreateReviewTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<IContentModerationService> _moderationServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _moderationServiceMock = new Mock<IContentModerationService>();

        // Setup ServiceProvider for background task
        var serviceScopeMock = new Mock<IServiceScope>();
        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        
        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        serviceScopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceScopeMock.Object);
        
        _serviceProviderMock.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactoryMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IContentModerationService))).Returns(_moderationServiceMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IApplicationDbContext))).Returns(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_ValidRequest_ShouldCreateReview()
    {
        // Arrange
        var user = new User { Id = 1, Username = "TestUser", Status = "Active" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new CreateReviewCommandHandler(_context, _serviceProviderMock.Object);
        var command = new CreateReviewCommand 
        { 
            ProductId = 101, 
            ReviewerId = 1, 
            Rating = 5, 
            Comment = "Excellent product!" 
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeGreaterThan(0);
        var review = await _context.Reviews.FindAsync(result);
        review.ShouldNotBeNull();
        review.Rating.ShouldBe(5);
        review.Comment.ShouldBe("Excellent product!");
        review.Status.ShouldBe("Visible");
    }

    [Test]
    public async Task Handle_LowRating_ShouldFlagReview()
    {
        // Arrange
        var user = new User { Id = 2, Username = "TestUser2", Status = "Active" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new CreateReviewCommandHandler(_context, _serviceProviderMock.Object);
        var command = new CreateReviewCommand 
        { 
            ProductId = 101, 
            ReviewerId = 2, 
            Rating = 1, 
            Comment = "Poor quality." 
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var review = await _context.Reviews.FindAsync(result);
        review!.FlaggedBySystem.ShouldBeTrue();
        review.FlagReason.ShouldBe("LowRating");
        review.Status.ShouldBe("PendingReview");
    }

    [Test]
    public void Handle_BannedUser_ShouldThrowException()
    {
        // Arrange
        var user = new User { Id = 3, Username = "BannedUser", Status = "Banned" };
        _context.Users.Add(user);
        _context.SaveChanges();

        var handler = new CreateReviewCommandHandler(_context, _serviceProviderMock.Object);
        var command = new CreateReviewCommand { ProductId = 101, ReviewerId = 3, Rating = 5 };

        // Act & Assert
        Should.ThrowAsync<Exception>(() => handler.Handle(command, CancellationToken.None))
            .Result.Message.ShouldBe("Tài khoản của bạn đã bị khóa.");
    }
}
