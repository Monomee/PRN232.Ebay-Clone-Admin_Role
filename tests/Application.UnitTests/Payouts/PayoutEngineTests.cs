using EbayClone.Infrastructure.Services;
using EbayClone.Application.UnitTests.Security;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Payouts.Commands.ReleaseHold;
using EbayClone.Application.Payouts.Commands.RunPayoutEngine;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Payouts;

/// <summary>
/// W7-20 — Payout Engine (chuyển tiền tự động cho seller, handler chính) và ReleaseHold.
/// Cổng thanh toán được giả lập bằng Moq để kết quả luôn xác định (bản thật thành công ngẫu nhiên 90%).
/// </summary>
public class PayoutEngineTests
{
    private const int SellerId = 5;
    private const string BankJson = "{\"bankName\":\"VCB\",\"accountNumber\":\"0123456789\",\"accountName\":\"NGUYEN VAN A\"}";

    private ApplicationDbContext _context = null!;
    private Mock<IMockPaymentGateway> _gatewayMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _gatewayMock = new Mock<IMockPaymentGateway>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private RunPayoutEngineCommandHandler CreateHandler() =>
        new(_context, _gatewayMock.Object, NullLogger<RunPayoutEngineCommandHandler>.Instance, EncryptionServiceTests.Create(), new SensitiveDataLogger(_context, EncryptionServiceTests.Create(), SensitiveLogTests.Actor("42", "SuperAdmin")));

    private void SetupGateway(bool success, string? error = null) =>
        _gatewayMock
            .Setup(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentResult(success, error));

    private async Task SeedSellerAsync(string status = "Active", string? bank = BankJson, decimal available = 500m, bool engineEnabled = true)
    {
        _context.PayoutConfigs.Add(new PayoutConfig { Id = 1, MinimumThreshold = 10m, IsEnabled = engineEnabled });
        _context.Users.Add(new User { Id = SellerId, Username = "seller", Role = "Seller", Status = status, BankAccountMock = bank == null ? null : EncryptionServiceTests.Create().Encrypt(bank) });
        _context.SellerWallets.Add(new SellerWallet { SellerId = SellerId, AvailableBalance = available });
        await _context.SaveChangesAsync();
    }

    // ── RunPayoutEngine: happy path ─────────────────────────────────────────

    [Test]
    public async Task TamperedNewBank_DoesNotLockFundsOrCallGateway()
    {
        await SeedSellerAsync();
        (await _context.Users.SingleAsync()).BankAccountMock = "gcm:v1:AAAA";
        await _context.SaveChangesAsync();
        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), default);
        result.Failed.ShouldBe(1);
        var wallet = await _context.SellerWallets.SingleAsync();
        wallet.AvailableBalance.ShouldBe(500m);
        wallet.LockedBalance.ShouldBe(0m);
        (await _context.PayoutTransactions.CountAsync()).ShouldBe(0);
        (await _context.SensitiveDataLogs.SingleAsync()).Action.ShouldBe("PayoutRejected");
        _gatewayMock.Verify(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task TamperedStaleSnapshot_UnlocksWithoutGateway_AndOtherSellersContinue()
    {
        await SeedSellerAsync(available: 300m);
        var wallet = await _context.SellerWallets.SingleAsync();
        wallet.LockedBalance = 200;
        (await _context.Users.SingleAsync()).BankAccountMock = "gcm:v1:AAAA";
        _context.PayoutTransactions.Add(new PayoutTransaction { SellerId = SellerId, Amount = 200,
            Status = PayoutTransaction.StatusProcessing, CreatedAt = DateTime.UtcNow.AddMinutes(-2), BankSnapshot = "gcm:v1:AAAA" });
        _context.Users.Add(new User { Id = 6, Username = "healthy", Role = "Seller", BankAccountMock = EncryptionServiceTests.Create().Encrypt(BankJson) });
        _context.SellerWallets.Add(new SellerWallet { SellerId = 6, AvailableBalance = 100 });
        await _context.SaveChangesAsync();
        SetupGateway(true);
        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), default);
        result.Success.ShouldBe(1);
        wallet.AvailableBalance.ShouldBe(500m);
        wallet.LockedBalance.ShouldBe(0m);
        var stale = await _context.PayoutTransactions.SingleAsync(p => p.SellerId == SellerId);
        stale.Status.ShouldBe(PayoutTransaction.StatusFailed);
        stale.ErrorLog.ShouldBe("Bank details failed verification; gateway was not called.");
        (await _context.SensitiveDataLogs.CountAsync(l => l.Action == "PayoutResult")).ShouldBe(2);
        _gatewayMock.Verify(g => g.ProcessAsync(200, It.IsAny<string>()), Times.Never);
        _gatewayMock.Verify(g => g.ProcessAsync(100, BankJson), Times.Once);
    }

    [Test]
    public async Task Run_GatewaySuccess_ShouldTransferAllAvailableFunds()
    {
        // Arrange
        await SeedSellerAsync();
        SetupGateway(success: true);

        // Act
        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), CancellationToken.None);

        // Assert
        result.TotalScanned.ShouldBe(1);
        result.Success.ShouldBe(1);
        result.Failed.ShouldBe(0);
        result.TotalDisbursed.ShouldBe(500m);

        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.AvailableBalance.ShouldBe(0m);
        wallet.LockedBalance.ShouldBe(0m);
        wallet.TotalWithdrawn.ShouldBe(500m);

        var payout = await _context.PayoutTransactions.SingleAsync();
        payout.Status.ShouldBe(PayoutTransaction.StatusSuccess);
        payout.Amount.ShouldBe(500m);
        payout.BankSnapshot.ShouldNotBe(BankJson);
        EncryptionServiceTests.Create().Decrypt(payout.BankSnapshot!).ShouldBe(BankJson);
        (await _context.SensitiveDataLogs.CountAsync()).ShouldBe(2);

        (await _context.FinancialTransactions.CountAsync(t => t.Type == "AutoPayout")).ShouldBe(1);
        _gatewayMock.Verify(g => g.ProcessAsync(500m, BankJson), Times.Once);
    }

    [Test]
    public async Task Run_GatewayFailed_ShouldReturnFundsToAvailable()
    {
        await SeedSellerAsync();
        SetupGateway(success: false, error: "Bank timeout");

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), CancellationToken.None);

        result.Success.ShouldBe(0);
        result.Failed.ShouldBe(1);
        result.TotalDisbursed.ShouldBe(0m);

        // Tiền không bị mất: trả lại Available, không bị kẹt ở Locked
        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.AvailableBalance.ShouldBe(500m);
        wallet.LockedBalance.ShouldBe(0m);

        var payout = await _context.PayoutTransactions.SingleAsync();
        payout.Status.ShouldBe(PayoutTransaction.StatusFailed);
        payout.ErrorLog.ShouldBe("Bank timeout");
    }

    [Test]
    public async Task Run_TargetedPartialAmount_ShouldPayOnlyRequestedAmount()
    {
        await SeedSellerAsync();
        SetupGateway(success: true);

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(SellerId, 200m), CancellationToken.None);

        result.Success.ShouldBe(1);
        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.AvailableBalance.ShouldBe(300m);
        wallet.TotalWithdrawn.ShouldBe(200m);
    }

    // ── RunPayoutEngine: validate ───────────────────────────────────────────

    [Test]
    public async Task Run_AmountExceedsAvailable_ShouldSkipWithoutCallingGateway()
    {
        await SeedSellerAsync();
        SetupGateway(success: true);

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(SellerId, 9999m), CancellationToken.None);

        result.Skipped.ShouldBe(1);
        (result.Message ?? string.Empty).ShouldContain("exceeds available balance");
        _gatewayMock.Verify(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Run_NoBankAccount_ShouldSkip()
    {
        await SeedSellerAsync(bank: null);
        SetupGateway(success: true);

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), CancellationToken.None);

        result.Skipped.ShouldBe(1);
        (await _context.PayoutTransactions.CountAsync()).ShouldBe(0);
        _gatewayMock.Verify(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    // ── RunPayoutEngine: quyền / rủi ro ─────────────────────────────────────

    [Test]
    public async Task Run_BannedSeller_ShouldNotReceivePayout()
    {
        await SeedSellerAsync(status: "Banned");
        SetupGateway(success: true);

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), CancellationToken.None);

        result.Skipped.ShouldBe(1);
        result.Success.ShouldBe(0);
        var wallet = await _context.SellerWallets.SingleAsync(w => w.SellerId == SellerId);
        wallet.AvailableBalance.ShouldBe(500m);
        _gatewayMock.Verify(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Run_AlreadyProcessing_ShouldSkipToAvoidDoublePayout()
    {
        await SeedSellerAsync();
        _context.PayoutTransactions.Add(new PayoutTransaction
        {
            SellerId = SellerId, Amount = 500m, Status = PayoutTransaction.StatusProcessing
        });
        await _context.SaveChangesAsync();
        SetupGateway(success: true);

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), CancellationToken.None);

        result.Skipped.ShouldBe(1);
        _gatewayMock.Verify(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Run_EngineDisabled_ShouldAbortScheduledRun()
    {
        await SeedSellerAsync(engineEnabled: false);
        SetupGateway(success: true);

        var result = await CreateHandler().Handle(new RunPayoutEngineCommand(), CancellationToken.None);

        result.TotalScanned.ShouldBe(0);
        _gatewayMock.Verify(g => g.ProcessAsync(It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    // ── ReleaseHold ─────────────────────────────────────────────────────────

    [Test]
    public async Task ReleaseHold_HoldTransaction_ShouldMarkReleased()
    {
        _context.PayoutTransactions.Add(new PayoutTransaction
        {
            Id = 1, SellerId = SellerId, Amount = 100m, Status = PayoutTransaction.StatusHold, ErrorLog = "Dispute open"
        });
        await _context.SaveChangesAsync();

        var result = await new ReleaseHoldCommandHandler(_context).Handle(new ReleaseHoldCommand(1), CancellationToken.None);

        result.ShouldBeTrue();
        var tx = await _context.PayoutTransactions.SingleAsync(t => t.Id == 1);
        tx.Status.ShouldBe(PayoutTransaction.StatusReleased);
        tx.CompletedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ReleaseHold_SuccessTransaction_ShouldNotBeReleased()
    {
        _context.PayoutTransactions.Add(new PayoutTransaction
        {
            Id = 1, SellerId = SellerId, Amount = 100m, Status = PayoutTransaction.StatusSuccess
        });
        await _context.SaveChangesAsync();

        var handler = new ReleaseHoldCommandHandler(_context);

        (await handler.Handle(new ReleaseHoldCommand(1), CancellationToken.None)).ShouldBeFalse();
        (await handler.Handle(new ReleaseHoldCommand(999), CancellationToken.None)).ShouldBeFalse();

        var tx = await _context.PayoutTransactions.SingleAsync(t => t.Id == 1);
        tx.Status.ShouldBe(PayoutTransaction.StatusSuccess);
    }
}
