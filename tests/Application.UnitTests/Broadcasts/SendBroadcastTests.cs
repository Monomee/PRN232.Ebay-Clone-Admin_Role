using EbayClone.Application.Broadcasts.Commands.SendBroadcast;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Broadcasts;

/// <summary>
/// W7-20 — SendBroadcastCommandHandler: admin gửi / hẹn giờ thông báo hàng loạt.
/// Các test chỉ dùng kênh InApp hoặc hẹn giờ, để không kích hoạt luồng gửi email chạy nền (Task.Run).
/// </summary>
public class SendBroadcastTests
{
    private const int AdminId = 99;

    private ApplicationDbContext _context = null!;
    private Mock<INotificationNotifier> _notifierMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _notifierMock = new Mock<INotificationNotifier>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private SendBroadcastCommandHandler CreateHandler() =>
        new(_context, _notifierMock.Object, _scopeFactoryMock.Object, NullLogger<SendBroadcastCommandHandler>.Instance,
            EbayClone.Application.UnitTests.Security.SensitiveLogTests.Actor("99", "SuperAdmin"));

    // ── Happy path ──────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_InAppToAll_ShouldSaveSentNotificationAndPushRealtime()
    {
        // Arrange
        var command = new SendBroadcastCommand
        {
            Title = "Bao tri he thong",
            Content = "He thong bao tri luc 2h sang",
            TargetAudience = "All",
            Channels = new List<string> { "InApp" },
            CreatedBy = 123
        };

        // Act
        var created = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        created.ShouldBe(1);

        var notification = await _context.Notifications.SingleAsync();
        notification.Title.ShouldBe("Bao tri he thong");
        notification.Type.ShouldBe("InApp");
        notification.Status.ShouldBe("Sent");
        notification.UserRole.ShouldBeNull();      // null = gửi cho tất cả
        notification.UserId.ShouldBeNull();
        notification.SentAt.ShouldNotBeNull();
        notification.CreatedBy.ShouldBe(AdminId);

        (await _context.AdminActions.CountAsync(a => a.Action == "SendBroadcast" && a.AdminId == AdminId)).ShouldBe(1);

        _notifierMock.Verify(n => n.NotifyNewNotificationAsync(
            notification.Id, "Bao tri he thong", "He thong bao tri luc 2h sang",
            null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_Scheduled_ShouldSaveScheduledWithoutPushing()
    {
        var scheduleAt = DateTime.UtcNow.AddDays(1);
        var command = new SendBroadcastCommand
        {
            Title = "Khuyen mai",
            Content = "Giam gia 50%",
            TargetAudience = "Seller",
            Channels = new List<string> { "InApp", "Email" },
            ScheduleAt = scheduleAt,
            CreatedBy = AdminId
        };

        var created = await CreateHandler().Handle(command, CancellationToken.None);

        // Mỗi kênh 1 bản ghi, chưa gửi
        created.ShouldBe(2);
        var notifications = await _context.Notifications.ToListAsync();
        notifications.Count.ShouldBe(2);
        notifications.ShouldAllBe(n => n.Status == "Scheduled" && n.SentAt == null && n.UserRole == "Seller");

        (await _context.AdminActions.CountAsync(a => a.Action == "ScheduleBroadcast")).ShouldBe(1);
        _notifierMock.Verify(n => n.NotifyNewNotificationAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_GroupAudience_ShouldStoreGroupRole()
    {
        var command = new SendBroadcastCommand
        {
            Title = "Thong bao nhom",
            Content = "Noi dung",
            TargetAudience = "Group",
            TargetGroup = "TopRated",
            Channels = new List<string> { "InApp" },
            CreatedBy = AdminId
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        var notification = await _context.Notifications.SingleAsync();
        notification.UserRole.ShouldBe("Group:TopRated");
    }

    // ── Validate ────────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_InvalidChannel_ShouldThrowAndSaveNothing()
    {
        var command = new SendBroadcastCommand
        {
            Title = "Test",
            Content = "Test",
            Channels = new List<string> { "SMS" },
            CreatedBy = AdminId
        };

        await Should.ThrowAsync<ArgumentException>(() => CreateHandler().Handle(command, CancellationToken.None));

        (await _context.Notifications.CountAsync()).ShouldBe(0);
        (await _context.AdminActions.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Handle_EmptyChannels_ShouldThrow()
    {
        var command = new SendBroadcastCommand
        {
            Title = "Test",
            Content = "Test",
            Channels = new List<string>(),
            CreatedBy = AdminId
        };

        await Should.ThrowAsync<ArgumentException>(() => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task Handle_InvalidAudience_ShouldThrow()
    {
        var command = new SendBroadcastCommand
        {
            Title = "Test",
            Content = "Test",
            TargetAudience = "Everyone",
            Channels = new List<string> { "InApp" },
            CreatedBy = AdminId
        };

        await Should.ThrowAsync<ArgumentException>(() => CreateHandler().Handle(command, CancellationToken.None));
    }

    // ── Đối tượng nhận / người chịu trách nhiệm ─────────────────────────────

    [Test]
    public async Task Handle_GroupWithoutTargetGroup_ShouldThrow()
    {
        var command = new SendBroadcastCommand
        {
            Title = "Test",
            Content = "Test",
            TargetAudience = "Group",
            TargetGroup = null,
            Channels = new List<string> { "InApp" },
            CreatedBy = AdminId
        };

        await Should.ThrowAsync<ArgumentException>(() => CreateHandler().Handle(command, CancellationToken.None));
        (await _context.Notifications.CountAsync()).ShouldBe(0);
    }
}
