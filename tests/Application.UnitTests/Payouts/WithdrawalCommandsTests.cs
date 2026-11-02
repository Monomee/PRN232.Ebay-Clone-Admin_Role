using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Withdrawals.Commands.ApproveWithdrawal;
using EbayClone.Application.Withdrawals.Commands.RejectWithdrawal;
using EbayClone.Application.Withdrawals.Commands.RequestWithdrawal;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using EbayClone.Application.UnitTests.Security;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Payouts;

/// <summary>
/// W7-20 — Rút tiền thủ công: seller tạo yêu cầu, admin duyệt (handler chính) hoặc từ chối.
/// Luồng tiền: Available --(Request)--> Locked --(Approve)--> TotalWithdrawn
///                                       Locked --(Reject)--> Available
/// </summary>
public class WithdrawalCommandsTests
{
    private const int AdminId = 99;
    private const int SellerId = 5;

    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userMock = new Mock<IUser>();
        _userMock.Setup(u => u.Id).Returns(AdminId.ToString());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    /// <summary>Seller đã yêu cầu rút 150$, số tiền này đang bị giam trong LockedBalance.</summary>
    private async Task SeedPendingWithdrawalAsync(string status = WithdrawalRequest.StatusPending)
    {
        _context.Users.Add(new User { Id = SellerId, Username = "seller", Role = "Seller" });
        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, AvailableBalance = 50m, LockedBalance = 150m });
        _context.WithdrawalRequests.Add(new WithdrawalRequest
        {
            Id = 1,
            SellerId = SellerId,
            Amount = 150m,
            BankName = EncryptionServiceTests.Create().Encrypt("VCB"),
            BankAccountNumber = EncryptionServiceTests.Create().Encrypt("0123456789"),
            BankAccountName = EncryptionServiceTests.Create().Encrypt("NGUYEN VAN A"),
            Status = status
        });
        await _context.SaveChangesAsync();
    }

    // ── Approve: happy path ─────────────────────────────────────────────────

    [Test]
    public async Task Approve_PendingWithdrawal_ShouldConfirmLockedFundsAndRecordTransaction()
    {
        // Arrange
        await SeedPendingWithdrawalAsync();
        var handler = new ApproveWithdrawalCommandHandler(_context, _userMock.Object, new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        // Act
        await handler.Handle(new ApproveWithdrawalCommand(1, "BANK-TX-001"), CancellationToken.None);

        // Assert
        var withdrawal = await _context.WithdrawalRequests.SingleAsync(w => w.Id == 1);
        withdrawal.Status.ShouldBe(WithdrawalRequest.StatusApproved);
        withdrawal.ProcessedBy.ShouldBe(AdminId);
        withdrawal.TransactionId.ShouldBe("BANK-TX-001");
        (await _context.SensitiveDataLogs.SingleAsync()).Action.ShouldBe("ApproveWithdrawal");

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(0m);
        wallet.AvailableBalance.ShouldBe(50m);
        wallet.TotalWithdrawn.ShouldBe(150m);

        var tx = await _context.FinancialTransactions.SingleAsync();
        tx.Type.ShouldBe("Withdrawal");
        tx.Amount.ShouldBe(-150m);
        tx.WithdrawalId.ShouldBe(1);
    }

    // ── Approve: validate ───────────────────────────────────────────────────

    [Test]
    public async Task Approve_NotFound_ShouldThrowKeyNotFound()
    {
        var handler = new ApproveWithdrawalCommandHandler(_context, _userMock.Object, new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        await Should.ThrowAsync<KeyNotFoundException>(
            () => handler.Handle(new ApproveWithdrawalCommand(999, "X"), CancellationToken.None));
    }

    // ── Approve: trạng thái (không duyệt 2 lần) ─────────────────────────────

    [Test]
    public async Task Approve_AlreadyApproved_ShouldThrowAndNotChangeWallet()
    {
        await SeedPendingWithdrawalAsync(WithdrawalRequest.StatusApproved);
        var handler = new ApproveWithdrawalCommandHandler(_context, _userMock.Object, new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new ApproveWithdrawalCommand(1, "BANK-TX-002"), CancellationToken.None));

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(150m);
        wallet.TotalWithdrawn.ShouldBe(0m);
        (await _context.FinancialTransactions.CountAsync()).ShouldBe(0);
    }

    // ── Reject ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Reject_PendingWithdrawal_ShouldReturnFundsToAvailable()
    {
        await SeedPendingWithdrawalAsync();
        var handler = new RejectWithdrawalCommandHandler(_context, _userMock.Object, new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        await handler.Handle(new RejectWithdrawalCommand(1, "Sai thong tin ngan hang"), CancellationToken.None);

        var withdrawal = await _context.WithdrawalRequests.SingleAsync(w => w.Id == 1);
        withdrawal.Status.ShouldBe(WithdrawalRequest.StatusRejected);
        withdrawal.RejectionReason.ShouldBe("Sai thong tin ngan hang");
        (await _context.SensitiveDataLogs.SingleAsync()).Action.ShouldBe("RejectWithdrawal");

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.LockedBalance.ShouldBe(0m);
        wallet.AvailableBalance.ShouldBe(200m);
    }

    // ── Request (seller tạo yêu cầu) ────────────────────────────────────────

    [Test]
    public async Task Request_EnoughBalance_ShouldLockFundsAndCreatePendingRequest()
    {
        // Arrange: user hiện tại là seller
        _userMock.Setup(u => u.Id).Returns(SellerId.ToString());
        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, AvailableBalance = 300m });
        await _context.SaveChangesAsync();
        var handler = new RequestWithdrawalCommandHandler(_context, _userMock.Object, EncryptionServiceTests.Create(), new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        // Act
        var id = await handler.Handle(new RequestWithdrawalCommand
        {
            Amount = 100m,
            BankName = "VCB",
            BankAccountNumber = "0123456789",
            BankAccountName = "NGUYEN VAN A"
        }, CancellationToken.None);

        // Assert
        id.ShouldBeGreaterThan(0);
        var withdrawal = await _context.WithdrawalRequests.SingleAsync(w => w.Id == id);
        withdrawal.Status.ShouldBe(WithdrawalRequest.StatusPending);
        withdrawal.Amount.ShouldBe(100m);

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.AvailableBalance.ShouldBe(200m);
        wallet.LockedBalance.ShouldBe(100m);
    }

    [Test]
    public async Task Request_InsufficientBalance_ShouldThrowAndNotCreateRequest()
    {
        _userMock.Setup(u => u.Id).Returns(SellerId.ToString());
        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, AvailableBalance = 30m });
        await _context.SaveChangesAsync();
        var handler = new RequestWithdrawalCommandHandler(_context, _userMock.Object, EncryptionServiceTests.Create(), new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(new RequestWithdrawalCommand
        {
            Amount = 100m,
            BankName = "VCB",
            BankAccountNumber = "0123456789",
            BankAccountName = "NGUYEN VAN A"
        }, CancellationToken.None));

        (await _context.WithdrawalRequests.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Request_NonPositiveAmount_ShouldThrowArgumentException()
    {
        _userMock.Setup(u => u.Id).Returns(SellerId.ToString());
        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, AvailableBalance = 300m });
        await _context.SaveChangesAsync();
        var handler = new RequestWithdrawalCommandHandler(_context, _userMock.Object, EncryptionServiceTests.Create(), new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), _userMock.Object));

        await Should.ThrowAsync<ArgumentException>(() => handler.Handle(new RequestWithdrawalCommand
        {
            Amount = 0m,
            BankName = "VCB",
            BankAccountNumber = "0123456789",
            BankAccountName = "NGUYEN VAN A"
        }, CancellationToken.None));
    }
}
