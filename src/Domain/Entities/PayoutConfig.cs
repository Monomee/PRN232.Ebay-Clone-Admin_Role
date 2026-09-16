using System;

namespace EbayClone.Domain.Entities;

/// <summary>
/// Admin-configurable settings for the Automated Payout Engine.
/// System uses the row with Id = 1 (singleton pattern).
/// </summary>
public class PayoutConfig
{
    public int Id { get; set; }

    /// <summary>"Daily" or "Weekly"</summary>
    public string Frequency { get; set; } = "Daily";

    /// <summary>Minimum available balance required to trigger a payout.</summary>
    public decimal MinimumThreshold { get; set; } = 10m;

    /// <summary>Hour of day (UTC) when the daily job fires. E.g., 2 = 2:00 AM UTC.</summary>
    public int ScheduledHourUtc { get; set; } = 2;

    /// <summary>Whether the engine is globally enabled or paused.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? UpdatedBy { get; set; } // Admin user ID
}
