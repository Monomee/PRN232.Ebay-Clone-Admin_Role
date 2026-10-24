using Ardalis.GuardClauses;
using EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Disputes;

/// <summary>
/// W7-20 — ResolveDisputeCommandHandler: admin ra quyết định cuối cùng cho một dispute.
/// </summary>
public class ResolveDisputeTests
{
    private const int AdminId = 99;
    private const int SellerId = 2;
    private const int BuyerId = 3;
    private const string LongNote = "Admin da xem xet bang chung cua ca hai ben va dua ra quyet dinh cuoi cung cho vu viec nay.";

    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
    private Mock<IDisputeNotifier> _notifierMock = null!;
    private Mock<IEmailService> _emailMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userMock = new Mock<IUser>();
        _userMock.Setup(u => u.Id).Returns(AdminId.ToString());
        _notifierMock = new Mock<IDisputeNotifier>();
        _emailMock = new Mock<IEmailService>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private ResolveDisputeCommandHandler CreateHandler() =>
        new(_context, _userMock.Object, NullLogger<ResolveDisputeCommandHandler>.Instance,
            _notifierMock.Object, _emailMock.Object);

    /// <summary>
    /// Dữ liệu mẫu: đơn 100$, seller nhận 90$, 100$ đang bị đóng băng trong DisputedBalance.
    /// </summary>
    private async Task SeedDisputeAsync(string status = DisputeStatuses.Escalated)
    {
        _context.Users.Add(new User { Id = SellerId, Username = "seller", Email = "seller@test.com", Role = "Seller" });
        _context.Users.Add(new User { Id = BuyerId, Username = "buyer", Email = "buyer@test.com", Role = "Buyer" });

        var order = new OrderTable { Id = 100, BuyerId = BuyerId, Status = "Disputed", TotalPrice = 100m, SellerEarnings = 90m };
        order.OrderItems.Add(new OrderItem { Product = new Product { Id = 10, SellerId = SellerId } });
        _context.OrderTables.Add(order);

        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, DisputedBalance = 100m, PendingBalance = 0m });

        _context.Disputes.Add(new Dispute
        {
            Id = 1,
            CaseId = "CASE-1",
            OrderId = 100,
            RaisedBy = BuyerId,
            Status = status,
            Amount = 100m
        });

        await _context.SaveChangesAsync();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_SellerWins_ShouldResolveAndReleaseFrozenFundsToSeller()
    {
        // Arrange
        await SeedDisputeAsync();
        var command = new ResolveDisputeCommand
        {
            DisputeId = 1,
            Winner = DisputeWinners.Seller,
            AdminNotes = LongNote,
            SendNotifications = false
        };

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBe(1);

        var dispute = await _context.Disputes.SingleAsync(d => d.Id == 1);
        dispute.Status.ShouldBe(DisputeStatuses.Resolved);
        dispute.Winner.ShouldBe(DisputeWinners.Seller);
        dispute.ResolvedBy.ShouldBe(AdminId);
        dispute.RefundAmount.ShouldBe(0m);

        var order = await _context.OrderTables.SingleAsync(o => o.Id == 100);
        order.Status.ShouldBe("Delivered");

        // 90$ (phần của seller) được trả về PendingBalance, phần còn lại vẫn nằm trong DisputedBalance
        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.PendingBalance.ShouldBe(90m);
        wallet.DisputedBalance.ShouldBe(10m);

        (await _context.AdminActions.CountAsync(a => a.Action == "ResolveDispute" && a.AdminId == AdminId)).ShouldBe(1);
        _notifierMock.Verify(n => n.NotifyDisputeResolvedAsync(
            1, "CASE-1", DisputeWinners.Seller, 0m, AdminId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_BuyerWins_ShouldRefundBuyerAndDeductSellerWallet()
    {
        // Arrange
        await SeedDisputeAsync();
        var command = new ResolveDisputeCommand
        {
            DisputeId = 1,
            Winner = DisputeWinners.Buyer,
            RefundAmount = 100m,
            AdminNotes = LongNote,
            AddSellerViolation = true,
            SendNotifications = false
        };

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        var order = await _context.OrderTables.SingleAsync(o => o.Id == 100);
        order.Status.ShouldBe("Refunded");
        order.SellerEarnings.ShouldBe(0m);

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.DisputedBalance.ShouldBe(0m);
        wallet.TotalRefunded.ShouldBe(100m);

        var refund = await _context.FinancialTransactions.SingleAsync();
        refund.Type.ShouldBe("Refund");
        refund.Amount.ShouldBe(100m);
        refund.SellerId.ShouldBe(SellerId);

        var seller = await _context.Users.SingleAsync(u => u.Id == SellerId);
        seller.ViolationCount.ShouldBe(1);
    }

    // ── Validate ────────────────────────────────────────────────────────────

    [Test]
    public void Validator_InvalidCommand_ShouldReturnErrors()
    {
        var validator = new ResolveDisputeCommandValidator();

        // Ghi chú quá ngắn (< 50 ký tự)
        var shortNote = validator.Validate(new ResolveDisputeCommand
        {
            DisputeId = 1, Winner = DisputeWinners.Seller, AdminNotes = "Qua ngan"
        });
        shortNote.IsValid.ShouldBeFalse();
        shortNote.Errors.ShouldContain(e => e.PropertyName == nameof(ResolveDisputeCommand.AdminNotes));

        // Winner không thuộc Buyer / Seller / Split
        var badWinner = validator.Validate(new ResolveDisputeCommand
        {
            DisputeId = 1, Winner = "Nobody", AdminNotes = LongNote
        });
        badWinner.Errors.ShouldContain(e => e.PropertyName == nameof(ResolveDisputeCommand.Winner));

        // Buyer thắng nhưng không nhập số tiền hoàn
        var missingRefund = validator.Validate(new ResolveDisputeCommand
        {
            DisputeId = 1, Winner = DisputeWinners.Buyer, AdminNotes = LongNote
        });
        missingRefund.Errors.ShouldContain(e => e.PropertyName == nameof(ResolveDisputeCommand.RefundAmount));

        // Lệnh hợp lệ
        var valid = validator.Validate(new ResolveDisputeCommand
        {
            DisputeId = 1, Winner = DisputeWinners.Buyer, RefundAmount = 50m, AdminNotes = LongNote
        });
        valid.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_DisputeNotFound_ShouldThrowNotFoundException()
    {
        var command = new ResolveDisputeCommand
        {
            DisputeId = 999, Winner = DisputeWinners.Seller, AdminNotes = LongNote, SendNotifications = false
        };

        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().Handle(command, CancellationToken.None));
    }

    // ── Quyền / trạng thái ──────────────────────────────────────────────────

    [Test]
    public async Task Handle_AlreadyResolved_ShouldThrowAndNotChangeWallet()
    {
        // Arrange: dispute đã được xử lý trước đó
        await SeedDisputeAsync(DisputeStatuses.Resolved);
        var command = new ResolveDisputeCommand
        {
            DisputeId = 1,
            Winner = DisputeWinners.Buyer,
            RefundAmount = 100m,
            AdminNotes = LongNote,
            SendNotifications = false
        };

        // Act + Assert: không cho xử lý lần 2 (tránh hoàn tiền 2 lần)
        await Should.ThrowAsync<ValidationException>(() => CreateHandler().Handle(command, CancellationToken.None));

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.DisputedBalance.ShouldBe(100m);
        (await _context.FinancialTransactions.CountAsync()).ShouldBe(0);
        _notifierMock.Verify(n => n.NotifyDisputeResolvedAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
