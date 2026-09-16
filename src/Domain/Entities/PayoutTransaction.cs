using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EbayClone.Domain.Entities;

/// <summary>
/// Records each individual automated payout attempt for a Seller.
/// Status lifecycle: Processing → Success | Failed | Hold
/// </summary>
public class PayoutTransaction
{
    public int Id { get; set; }

    public int SellerId { get; set; }

    public decimal Amount { get; set; }

    // ─── Status constants ────────────────────────────────────────────────────
    public const string StatusProcessing = "Processing"; // Funds locked, gateway call in progress
    public const string StatusSuccess    = "Success";    // Payment gateway confirmed
    public const string StatusFailed     = "Failed";     // Gateway error, funds returned to Available
    public const string StatusHold       = "Hold";       // Blocked by dispute or risk check
    public const string StatusReleased   = "Released";   // Admin released a Hold; will retry next run
    public const string StatusRefund     = "Refund";     // Refund to buyer from seller's wallet

    public string Status { get; set; } = StatusProcessing;

    /// <summary>Human-readable reason for Failed or Hold status.</summary>
    public string? ErrorLog { get; set; }

    /// <summary>Snapshot of the bank info at the time of payout (JSON).</summary>
    public string? BankSnapshot { get; set; }

    /// <summary>Session identifier that grouped this payout run (e.g. "2026-03-20T21:00:00Z").</summary>
    public string? SessionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    // ─── Navigation ──────────────────────────────────────────────────────────
    [ForeignKey(nameof(SellerId))]
    public virtual User? Seller { get; set; }
}
