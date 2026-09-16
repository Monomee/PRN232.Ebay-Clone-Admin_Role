using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Commands.BanUser;
using EbayClone.Application.Users.Commands.UnbanUser;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;
using MediatR;
using EbayClone.Application.Sellers.Queries.GetSellerPerformanceMetrics;

namespace EbayClone.Application.UnitTests.Users;

public class UserManagementTests
{
    private ApplicationDbContext _context = null!;
    private Mock<ISellerHubService> _sellerHubServiceMock = null!;
    private Mock<ISender> _senderMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sellerHubServiceMock = new Mock<ISellerHubService>();
        _senderMock = new Mock<ISender>();

        _senderMock.Setup(s => s.Send(It.IsAny<GetSellerPerformanceMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SellerPerformanceMetricsDto>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_BanUser_ShouldUpdateStatusAndLogAction()
    {
        // Arrange
        var user = new EbayClone.Domain.Entities.User { Id = 1, Username = "BadUser", Status = "Active" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new BanUserCommandHandler(_context, _sellerHubServiceMock.Object, _senderMock.Object);
        var command = new BanUserCommand { UserId = 1, Reason = "Violation", AdminId = 99 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        var updatedUser = await _context.Users.FindAsync(1);
        updatedUser!.Status.ShouldBe("Banned");
        updatedUser.BannedReason.ShouldBe("Violation");
        updatedUser.BannedBy.ShouldBe(99);

        var action = await _context.AdminActions.FirstOrDefaultAsync(a => a.TargetId == 1 && a.Action == "BanUser");
        action.ShouldNotBeNull();
        
        var notifications = await _context.Notifications.Where(n => n.UserId == 1).ToListAsync();
        notifications.Count.ShouldBe(2); // InApp and Email
    }

    [Test]
    public async Task Handle_UnbanUser_ShouldRestoreStatus()
    {
        // Arrange
        var user = new EbayClone.Domain.Entities.User { Id = 2, Username = "GoodUser", Status = "Banned", BannedReason = "Fixed" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new UnbanUserCommandHandler(_context, _sellerHubServiceMock.Object, _senderMock.Object);
        var command = new UnbanUserCommand { UserId = 2, AdminId = 99 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        var updatedUser = await _context.Users.FindAsync(2);
        updatedUser!.Status.ShouldBe("Active");
        updatedUser.BannedReason.ShouldBeNull();
        updatedUser.BannedBy.ShouldBeNull();
    }
}
