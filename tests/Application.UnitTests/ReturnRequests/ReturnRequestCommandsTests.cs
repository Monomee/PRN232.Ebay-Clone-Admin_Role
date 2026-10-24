using Ardalis.GuardClauses;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.ReturRequests.Commands.ApproveReturnRequest;
using EbayClone.Application.ReturRequests.Commands.FreezeReturnRequest;
using EbayClone.Application.ReturRequests.Commands.RejectReturnRequest;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.ReturnRequests;

/// <summary>
/// W7-20 — Admin xử lý yêu cầu trả hàng: Approve (handler chính), Reject, Freeze.
/// </summary>
public class ReturnRequestCommandsTests
{
    private const int AdminId = 99;
    private const int SellerId = 2;
    private const int BuyerId = 3;

    private ApplicationDbContext _context = null!;
    private Mock<INotificationNotifier> _notifierMock = null!;
    private Mock<IUser> _userMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _notifierMock = new Mock<INotificationNotifier>();
        _userMock = new Mock<IUser>();
        _userMock.Setup(u => u.Id).Returns(AdminId.ToString());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Dữ liệu mẫu: đơn 60$, seller nhận 50$ (= số tiền hoàn), ví seller đang giam LockedBalance.
    /// </summary>
    private async Task SeedReturnRequestAsync(string status = ReturnStatuses.Pending, decimal lockedBalance = 80m)
    {
        _context.Users.Add(new User { Id = SellerId, Username = "seller", Role = "Seller" });
        _context.Users.Add(new User { Id = BuyerId, Username = "buyer", Role = "Buyer" });

        var order = new OrderTable { Id = 100, BuyerId = BuyerId, Status = "Delivered", TotalPrice = 60m, SellerEarnings = 50m };
        order.OrderItems.Add(new OrderItem { Product = new Product { Id = 10, SellerId = SellerId } });
        _context.OrderTables.Add(order);

        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, LockedBalance = lockedBalance });

        _context.ReturnRequests.Add(new ReturnRequest
        {
            Id = 1,
            OrderId = 100,
            UserId = BuyerId,
            Reason = "San pham bi loi",
            Status = status
        });

        await _context.SaveChangesAsync();
    }

    private ApproveReturnRequestCommandHandler CreateApproveHandler() =>
        new(_context, _notifierMock.Object, _userMock.Object);

    // ── Approve: happy path ─────────────────────────────────────────────────

    [Test]
    public async Task Approve_KeepItem_ShouldRefundFromSellerLockedBalance()
    {
        // Arrange
        await SeedReturnRequestAsync();
        var command = new ApproveReturnRequestCommand(1, "Dong y hoan tien", "KeepItem");

        // Act
        await CreateApproveHandler().Handle(command, CancellationToken.None);

        // Assert
        var request = await _context.ReturnRequests.SingleAsync(r => r.Id == 1);
        request.Status.ShouldBe(ReturnStatuses.Approved);
        request.ResolvedByAdminId.ShouldBe(AdminId);
        request.ResolvedAt.ShouldNotBeNull();

        var order = await _context.OrderTables.SingleAsync(o => o.Id == 100);
        order.Status.ShouldBe("Refunded");

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(30m);
        wallet.TotalRefunded.ShouldBe(50m);

        var payout = await _context.PayoutTransactions.SingleAsync();
        payout.Status.ShouldBe(PayoutTransaction.StatusRefund);
        payout.Amount.ShouldBe(50m);

        (await _context.AdminActions.CountAsync(a => a.Action == "ApproveReturnRequest" && a.AdminId == AdminId)).ShouldBe(1);
        _notifierMock.Verify(n => n.NotifyReturnRequestUpdatedAsync(1, ReturnStatuses.Approved, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Approve_RequireReturn_ShouldWaitForReturnLabelWithoutRefund()
    {
        // Arrange
        await SeedReturnRequestAsync();
        var command = new ApproveReturnRequestCommand(1, "Can gui tra hang", "RequireReturn");

        // Act
        await CreateApproveHandler().Handle(command, CancellationToken.None);

        // Assert: chưa hoàn tiền cho đến khi buyer gửi trả hàng
        var request = await _context.ReturnRequests.SingleAsync(r => r.Id == 1);
        request.Status.ShouldBe(ReturnStatuses.WaitingForReturnLabel);

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(80m);
        (await _context.PayoutTransactions.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Approve_RefundedByEbayFund_ShouldNotTouchSellerWallet()
    {
        await SeedReturnRequestAsync();
        var command = new ApproveReturnRequestCommand(1, "eBay chiu chi phi", "KeepItem", IsRefundedByEbayFund: true);

        await CreateApproveHandler().Handle(command, CancellationToken.None);

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(80m);
        wallet.TotalRefunded.ShouldBe(0m);
        (await _context.PayoutTransactions.CountAsync()).ShouldBe(0);
    }

    // ── Approve: validate ───────────────────────────────────────────────────

    [Test]
    public void Approve_Validator_InvalidId_ShouldReturnError()
    {
        var result = new ApproveReturnRequestCommandValidator().Validate(new ApproveReturnRequestCommand(0, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ApproveReturnRequestCommand.ReturnRequestId));
    }

    [Test]
    public async Task Approve_NotFound_ShouldThrowNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(
            () => CreateApproveHandler().Handle(new ApproveReturnRequestCommand(999, null), CancellationToken.None));
    }

    [Test]
    public async Task Approve_InsufficientLockedBalance_ShouldThrowAndNotCreatePayout()
    {
        // Arrange: seller chỉ còn 10$ bị giam, cần hoàn 50$
        await SeedReturnRequestAsync(lockedBalance: 10m);

        // Act + Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateApproveHandler().Handle(new ApproveReturnRequestCommand(1, null, "KeepItem"), CancellationToken.None));

        (await _context.PayoutTransactions.CountAsync()).ShouldBe(0);
        _notifierMock.Verify(n => n.NotifyReturnRequestUpdatedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Approve: trạng thái (không xử lý lại yêu cầu đã đóng) ───────────────

    [Test]
    public async Task Approve_AlreadyRejected_ShouldThrowInvalidOperation()
    {
        await SeedReturnRequestAsync(ReturnStatuses.Rejected);

        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateApproveHandler().Handle(new ApproveReturnRequestCommand(1, null), CancellationToken.None));

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(80m);
    }

    // ── Reject ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Reject_PendingRequest_ShouldSetRejectedWithNote()
    {
        await SeedReturnRequestAsync();
        var handler = new RejectReturnRequestCommandHandler(_context, _notifierMock.Object, _userMock.Object);

        await handler.Handle(new RejectReturnRequestCommand(1, "Khong du bang chung"), CancellationToken.None);

        var request = await _context.ReturnRequests.SingleAsync(r => r.Id == 1);
        request.Status.ShouldBe(ReturnStatuses.Rejected);
        request.AdminNote.ShouldBe("Khong du bang chung");
        request.ResolvedByAdminId.ShouldBe(AdminId);
        (await _context.AdminActions.CountAsync(a => a.Action == "RejectReturnRequest")).ShouldBe(1);
    }

    [Test]
    public void Reject_Validator_MissingNote_ShouldReturnError()
    {
        var result = new RejectReturnRequestCommandValidator().Validate(new RejectReturnRequestCommand(1, ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(RejectReturnRequestCommand.AdminNote));
    }

    // ── Freeze ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Freeze_PendingRequest_ShouldSetFrozen()
    {
        await SeedReturnRequestAsync();
        var handler = new FreezeReturnRequestCommandHandler(_context, _notifierMock.Object, _userMock.Object);

        await handler.Handle(new FreezeReturnRequestCommand(1, "Nghi ngo gian lan"), CancellationToken.None);

        var request = await _context.ReturnRequests.SingleAsync(r => r.Id == 1);
        request.Status.ShouldBe(ReturnStatuses.Frozen);
        request.AdminNote.ShouldBe("[FROZEN] Nghi ngo gian lan");
    }

    [Test]
    public async Task Freeze_AlreadyApproved_ShouldThrowInvalidOperation()
    {
        await SeedReturnRequestAsync(ReturnStatuses.Approved);
        var handler = new FreezeReturnRequestCommandHandler(_context, _notifierMock.Object, _userMock.Object);

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new FreezeReturnRequestCommand(1, "Thu dong bang"), CancellationToken.None));
    }
}
