using EbayClone.Application.Common.Interfaces;
using System.Security.Cryptography;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Application.Payouts.Commands.RunPayoutEngine;

/// <summary>
/// Trigger the Payout Engine for all eligible Sellers.
/// Returns a PayoutEngineResult summarizing the session.
/// </summary>
public class RunPayoutEngineCommand : IRequest<PayoutEngineResult>
{
    public int? SellerId { get; set; }
    public decimal AmountToWithdraw { get; set; } = -1; // -1 means all available funds

    public RunPayoutEngineCommand() { }

    public RunPayoutEngineCommand(int? sellerId, decimal amountToWithdraw)
    {
        SellerId = sellerId;
        AmountToWithdraw = amountToWithdraw;
    }
}

public record PayoutEngineResult(
    int TotalScanned,
    int Success,
    int Failed,
    int OnHold,
    int Skipped,
    decimal TotalDisbursed,
    string SessionId,
    string? Message = null
);

public class RunPayoutEngineCommandHandler : IRequestHandler<RunPayoutEngineCommand, PayoutEngineResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryption;
    private readonly ISensitiveDataLogger _sensitiveLog;

    private readonly IMockPaymentGateway _gateway;
    private readonly ILogger<RunPayoutEngineCommandHandler> _logger;

    public RunPayoutEngineCommandHandler(
        IApplicationDbContext context,
        IMockPaymentGateway gateway,
        ILogger<RunPayoutEngineCommandHandler> logger, IEncryptionService encryption, ISensitiveDataLogger sensitiveLog)
    {
        _context = context;
        _encryption = encryption;
        _sensitiveLog = sensitiveLog;

        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PayoutEngineResult> Handle(RunPayoutEngineCommand request, CancellationToken cancellationToken)
    {
        var sessionId = DateTime.UtcNow.ToString("o");
        _logger.LogInformation("===== PAYOUT ENGINE SESSION START ===== | SessionId: {Session}", sessionId);

        // ── 0. Load the payout configuration ─────────────────────────────────
        var config = await _context.PayoutConfigs
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? new PayoutConfig();  // fallback to defaults if no row

        int successCount = 0, failedCount = 0, holdCount = 0, skippedCount = 0;
        decimal totalDisbursed = 0m;
        string? diagnosticMessage = null;

        var resumed = await ResumeStaleProcessingAsync(request.SellerId, sessionId, cancellationToken);
        successCount += resumed.Success;
        failedCount += resumed.Failed;
        totalDisbursed += resumed.Disbursed;

        if (!config.IsEnabled && !request.SellerId.HasValue)
        {
            _logger.LogInformation("[INFO] Payout Engine is DISABLED by admin. Aborting.");
            return new PayoutEngineResult(
                0, successCount, failedCount, holdCount, skippedCount, totalDisbursed, sessionId,
                resumed.Success + resumed.Failed > 0
                    ? "Finished stale payouts. Engine is disabled."
                    : null);
        }

        _logger.LogInformation("[INFO] Config: Frequency={Frequency}, MinThreshold=${Min}",
            config.Frequency, config.MinimumThreshold);

        // ── 1. Load all Seller wallets with their Sellers ────────────────────
        var query = _context.SellerWallets
            .Include(w => w.Seller)
            .AsQueryable();

        if (request.SellerId.HasValue)
        {
            _logger.LogInformation("[INFO] TARGETED RUN for SellerId: {Id}", request.SellerId.Value);
            query = query.Where(w => w.SellerId == request.SellerId.Value);
        }
        else
        {
            query = query.Where(w => w.AvailableBalance > config.MinimumThreshold);
        }

        var wallets = await query.ToListAsync(cancellationToken);

        _logger.LogInformation("[INFO] Total sellers with eligible balance: {Count}", wallets.Count);

        foreach (var wallet in wallets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Payout engine stopping before locking more sellers.");
                break;
            }

            var seller = wallet.Seller;
            if (seller == null) 
            { 
                if (request.SellerId.HasValue) diagnosticMessage = "Seller record not found.";
                skippedCount++; 
                continue; 
            }

            // ── CONCURRENCY GUARD: skip if a Processing payout already exists ─
            bool alreadyProcessing = await _context.PayoutTransactions
                .AnyAsync(p => p.SellerId == seller.Id
                            && p.Status == PayoutTransaction.StatusProcessing,
                    cancellationToken);

            if (alreadyProcessing)
            {
                _logger.LogWarning("[SKIP] Seller #{Id} ({Name}): Already has a Processing payout. Skipping to avoid double-payout.",
                    seller.Id, seller.Username);
                if (request.SellerId.HasValue) diagnosticMessage = "Already has a 'Processing' payout session active.";
                skippedCount++;
                continue;
            }

            // ── STEP 2: Risk checks ──────────────────────────────────────────
            if (seller.Status is "Suspended" or "Banned")
            {
                _logger.LogInformation("[SKIP] Seller #{Id} ({Name}): Account {Status}.",
                    seller.Id, seller.Username, seller.Status);
                if (request.SellerId.HasValue) diagnosticMessage = $"Account is {seller.Status}.";
                skippedCount++;
                continue;
            }

            // ── STEP 1 Bank info validation ───────────────────────────────────
            if (string.IsNullOrWhiteSpace(seller.BankAccountMock))
            {
                _logger.LogInformation("[SKIP] Seller #{Id} ({Name}): No bank account linked.",
                    seller.Id, seller.Username);
                if (request.SellerId.HasValue) diagnosticMessage = "No bank account information linked to this seller.";
                skippedCount++;
                continue;
            }

            var amountToPay = (request.AmountToWithdraw >= 0) 
                ? request.AmountToWithdraw 
                : wallet.AvailableBalance;

            _logger.LogCritical("[PAYOUT_DEBUG] CALC: OverrideVal={Val}, WalletAvail={Av}, FinalToPay={Final}", 
                request.AmountToWithdraw, wallet.AvailableBalance, amountToPay);

            if (amountToPay > wallet.AvailableBalance)
            {
                if (request.SellerId.HasValue) diagnosticMessage = $"Requested amount (${amountToPay:F2}) exceeds available balance (${wallet.AvailableBalance:F2}).";
                skippedCount++;
                continue;
            }

            // Authenticate before persisting a lock; a damaged bank must not strand funds.
            try { _encryption.Decrypt(seller.BankAccountMock); }
            catch (CryptographicException)
            {
                failedCount++;
                if (request.SellerId.HasValue) diagnosticMessage = "Bank details failed verification.";
                _sensitiveLog.Add("PayoutRejected", "Seller", seller.Id.ToString(), "Financial", new { sessionId, code = "BankDataIntegrity" });
                await _context.SaveChangesAsync(cancellationToken);
                continue;
            }

            // ── STEP 3: Lock funds → mark as Processing ───────────────────────
            _logger.LogCritical("[PAYOUT_DEBUG] LOCKING: Amount={A}, CurrentAvailable={Av}", amountToPay, wallet.AvailableBalance);
            wallet.LockAvailableFunds(amountToPay); // AvailableBalance → LockedBalance
            _logger.LogCritical("[PAYOUT_DEBUG] LOCKED: Available={Av}, Locked={Lv}", wallet.AvailableBalance, wallet.LockedBalance);

            var payoutTx = new PayoutTransaction
            {
                SellerId = seller.Id,
                Amount = amountToPay,
                Status = PayoutTransaction.StatusProcessing,
                BankSnapshot = seller.BankAccountMock,
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };
            _context.PayoutTransactions.Add(payoutTx);

            _sensitiveLog.Add("PayoutStarted", "Seller", seller.Id.ToString(), "Financial", new { amount = amountToPay, sessionId });

            // Flush the lock to DB before calling gateway (ensures idempotency across concurrent runs)
            await _context.SaveChangesAsync(cancellationToken);

            var finalized = await FinalizePayoutAsync(wallet, seller, payoutTx, amountToPay, sessionId);
            if (finalized)
            {
                totalDisbursed += amountToPay;
                successCount++;
                if (request.SellerId.HasValue) diagnosticMessage = "Payout successful.";
            }
            else
            {
                failedCount++;
                if (request.SellerId.HasValue) diagnosticMessage = $"FAILED: {payoutTx.ErrorLog}";
            }
        }

        if (request.SellerId.HasValue && wallets.Count == 0 && diagnosticMessage == null)
        {
            diagnosticMessage = "Seller has no available balance or balance is below minimum threshold.";
        }

        _logger.LogInformation(
            "[INFO) Summary: {S} Success | {F} Failed | {H} Hold | {K} Skipped | TotalDisbursed: ${D:F2}",
            successCount, failedCount, holdCount, skippedCount, totalDisbursed);
        _logger.LogInformation("===== PAYOUT ENGINE SESSION END =====");

        return new PayoutEngineResult(
            wallets.Count, successCount, failedCount, holdCount, skippedCount, totalDisbursed, sessionId, diagnosticMessage);
    }

    /// <summary>
    /// A Processing row older than the pod grace period has no live owner.
    /// The mock gateway died with that process, so calling it again does not pay twice.
    /// </summary>
    private async Task<ResumeTally> ResumeStaleProcessingAsync(
        int? sellerId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-45);
        var query = _context.PayoutTransactions
            .Include(p => p.Seller)
            .Where(p => p.Status == PayoutTransaction.StatusProcessing && p.CreatedAt < cutoff);

        if (sellerId.HasValue)
            query = query.Where(p => p.SellerId == sellerId.Value);

        var stale = await query.ToListAsync(cancellationToken);
        var tally = new ResumeTally();

        foreach (var payoutTx in stale)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var seller = payoutTx.Seller;
            if (seller == null)
            {
                _logger.LogWarning("Stale payout #{Id} has no seller. Leaving it Processing.", payoutTx.Id);
                continue;
            }

            var wallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == payoutTx.SellerId, cancellationToken);
            if (wallet == null)
            {
                _logger.LogWarning("Stale payout #{Id} has no wallet. Leaving it Processing.", payoutTx.Id);
                continue;
            }

            _logger.LogInformation(
                "Resuming stale payout #{Id} for seller #{SellerId}, created {CreatedAt:o}.",
                payoutTx.Id, payoutTx.SellerId, payoutTx.CreatedAt);

            var finalized = await FinalizePayoutAsync(
                wallet,
                seller,
                payoutTx,
                payoutTx.Amount,
                payoutTx.SessionId ?? sessionId);

            if (finalized)
            {
                tally.Success++;
                tally.Disbursed += payoutTx.Amount;
            }
            else
            {
                tally.Failed++;
            }
        }

        return tally;
    }

    /// <summary>
    /// Gateway then the second save. The save ignores shutdown cancellation so a seller
    /// already locked is not left in Processing when the host is stopping.
    /// </summary>
    private async Task<bool> FinalizePayoutAsync(
        SellerWallet wallet,
        User seller,
        PayoutTransaction payoutTx,
        decimal amount,
        string sessionId)
    {
        var bank = string.IsNullOrWhiteSpace(payoutTx.BankSnapshot)
            ? seller.BankAccountMock
            : payoutTx.BankSnapshot;

        string? bankInfo = null;
        try { if (bank != null) bankInfo = _encryption.Decrypt(bank); }
        catch (CryptographicException)
        {
            _logger.LogWarning("Bank verification failed for payout #{PayoutId}; gateway will not be called.", payoutTx.Id);
        }
        var gatewayResult = bankInfo == null
            ? new PaymentResult(false, "Bank details failed verification; gateway was not called.")
            : await _gateway.ProcessAsync(amount, bankInfo);
        _logger.LogCritical("[PAYOUT_DEBUG] GATEWAY: Success={S}, Error={E}", gatewayResult.Success, gatewayResult.ErrorMessage);

        if (gatewayResult.Success)
        {
            wallet.ConfirmWithdrawal(amount);
            payoutTx.Status = PayoutTransaction.StatusSuccess;
            payoutTx.CompletedAt = DateTime.UtcNow;

            _context.FinancialTransactions.Add(new FinancialTransaction
            {
                SellerId = seller.Id,
                UserId = seller.Id,
                Type = "AutoPayout",
                Amount = amount,
                BalanceAfter = wallet.AvailableBalance,
                Description = $"[AutoPayout] Session {sessionId} – transferred ${amount:F2}",
                Date = DateTime.UtcNow
            });

            _logger.LogInformation("[SUCCESS] Seller #{Id} ({Name}): ${Amount} → bank ✓",
                seller.Id, seller.Username, amount);
        }
        else
        {
            wallet.UnlockFunds(amount);
            payoutTx.Status = PayoutTransaction.StatusFailed;
            payoutTx.ErrorLog = gatewayResult.ErrorMessage;
            payoutTx.CompletedAt = DateTime.UtcNow;

            _logger.LogError("[FAILED] Seller #{Id} ({Name}): ${Amount} → {Error}",
                seller.Id, seller.Username, amount, gatewayResult.ErrorMessage);
        }

        _sensitiveLog.Add("PayoutResult", "PayoutTransaction", payoutTx.Id.ToString(), "Financial", new { payoutTx.SellerId, payoutTx.Amount, payoutTx.Status, sessionId });
        await _context.SaveChangesAsync(CancellationToken.None);
        return gatewayResult.Success;
    }

    private sealed class ResumeTally
    {
        public int Success { get; set; }
        public int Failed { get; set; }
        public decimal Disbursed { get; set; }
    }
}
