using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Reviews.Commands;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Reviews;

public class UpdateReviewStatusTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IEmailService> _emailMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _emailMock = new Mock<IEmailService>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_KeepAction_ShouldUpdateStatusAndNotPenalize()
    {
        // Arrange
        var reviewer = new User { Id = 1, Email = "user@test.com", ReviewViolationCount = 0 };
        _context.Users.Add(reviewer);

        var review = new Review { Id = 1, ReviewerId = 1, Status = "PendingReview" };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var handler = new UpdateReviewStatusCommandHandler(_context, _emailMock.Object);
        var command = new UpdateReviewStatusCommand { Id = 1, Status = "Visible", Action = "Keep", AdminId = 99 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        var updatedReview = await _context.Reviews.FindAsync(1);
        updatedReview!.Status.ShouldBe("Visible");
        updatedReview.ModeratedBy.ShouldBe(99);

        var updatedUser = await _context.Users.FindAsync(1);
        updatedUser!.ReviewViolationCount.ShouldBe(0);
        _emailMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Handle_HideAction_FirstViolation_ShouldSendWarning()
    {
        // Arrange
        var reviewer = new User { Id = 2, Email = "user2@test.com", ReviewViolationCount = 0 };
        _context.Users.Add(reviewer);

        var review = new Review { Id = 2, ReviewerId = 2, Status = "PendingReview" };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var handler = new UpdateReviewStatusCommandHandler(_context, _emailMock.Object);
        var command = new UpdateReviewStatusCommand { Id = 2, Status = "Hidden", Action = "Hide", AdminId = 99 };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedUser = await _context.Users.FindAsync(2);
        updatedUser!.ReviewViolationCount.ShouldBe(1);
        _emailMock.Verify(e => e.SendEmailAsync("user2@test.com", "CẢNH BÁO VI PHẠM ĐÁNH GIÁ SẢN PHẨM", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Handle_HideAction_SecondViolation_ShouldBanFor7Days()
    {
        // Arrange
        var reviewer = new User { Id = 3, Email = "user3@test.com", ReviewViolationCount = 1 };
        _context.Users.Add(reviewer);

        var review = new Review { Id = 3, ReviewerId = 3, Status = "PendingReview" };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var handler = new UpdateReviewStatusCommandHandler(_context, _emailMock.Object);
        var command = new UpdateReviewStatusCommand { Id = 3, Status = "Hidden", Action = "Hide", AdminId = 99 };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedUser = await _context.Users.FindAsync(3);
        updatedUser!.ReviewViolationCount.ShouldBe(2);
        updatedUser.ReviewBanUntil.ShouldNotBeNull();
        updatedUser.ReviewBanUntil.Value.ShouldBeGreaterThan(DateTime.UtcNow.AddDays(6));
        _emailMock.Verify(e => e.SendEmailAsync("user3@test.com", "TẠM KHÓA CHỨC NĂNG ĐÁNH GIÁ 7 NGÀY", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Handle_HideAction_FourthViolation_ShouldBanUser()
    {
        // Arrange
        var reviewer = new User { Id = 4, Email = "user4@test.com", ReviewViolationCount = 3 };
        _context.Users.Add(reviewer);

        var review = new Review { Id = 4, ReviewerId = 4, Status = "PendingReview" };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var handler = new UpdateReviewStatusCommandHandler(_context, _emailMock.Object);
        var command = new UpdateReviewStatusCommand { Id = 4, Status = "Hidden", Action = "Hide", AdminId = 99 };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedUser = await _context.Users.FindAsync(4);
        updatedUser!.ReviewViolationCount.ShouldBe(4);
        updatedUser.Status.ShouldBe("Banned");
        _emailMock.Verify(e => e.SendEmailAsync("user4@test.com", "TÀI KHOẢN ĐÃ BỊ KHÓA VĨNH VIỄN", It.IsAny<string>()), Times.Once);
    }
}
