using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users.Risk;

public record MonitorResult(int LinkWarnings, int LinkRestrictions, int ListingSpikes);

/// Watches accounts after sign-up, when they already passed the first check.
///
/// Enforcement ladder (mildest first). The monitor only ever applies the first three, automatically and reversibly;
/// suspending or banning a real customer stays an admin decision (existing ban / status endpoints):
///   1. Warning           - notification to the user, score shown to admins
///   2. RestrictListings  - ProductBanUntil: cannot list new products for a while
///   3. HoldPayouts       - PayoutHoldUntil: payout engine skips the seller
///   4. Suspended         - admin
///   5. Banned            - admin
/// Admins undo 2 and 3 with POST /api/Users/{id}/lift-restrictions.
public static class AccountMonitor
{
    public static readonly TimeSpan LinkRestriction = TimeSpan.FromDays(7);
    public static readonly TimeSpan LinkPayoutHold = TimeSpan.FromDays(30);
    public static readonly TimeSpan SpikeRestriction = TimeSpan.FromDays(1);
    public const int SpikeMinimum = 20;   // listings in 24h before the rule looks at all
    public const int SpikeFactor = 5;     // ... and at least 5x the seller's usual daily volume

    public static async Task<MonitorResult> RunAsync(IApplicationDbContext db, IEncryptionService encryption,
        ISensitiveDataLogger log, DateTime now, CancellationToken ct)
    {
        var (warnings, restrictions) = await LinksToBannedAccountsAsync(db, encryption, log, now, ct);
        var spikes = await ListingSpikesAsync(db, log, now, ct);
        await db.SaveChangesAsync(ct);
        return new MonitorResult(warnings, restrictions, spikes);
    }

    /// An approved account that turns out to share identity data with a banned one (a bank account or phone added
    /// later, or the other account was banned after this one signed up).
    private static async Task<(int, int)> LinksToBannedAccountsAsync(IApplicationDbContext db, IEncryptionService encryption,
        ISensitiveDataLogger log, DateTime now, CancellationToken ct)
    {
        var bannedIds = db.Users.Where(AccountLinking.BannedFilter).Select(u => u.Id);
        var bannedHashes = db.UserIdentifiers.Where(i => bannedIds.Contains(i.UserId)).Select(i => i.ValueHash);
        var candidateIds = db.UserIdentifiers
            .Where(i => bannedHashes.Contains(i.ValueHash) && !bannedIds.Contains(i.UserId))
            .Select(i => i.UserId).Distinct();
        var candidates = await db.Users.Include(u => u.Addresses)
            .Where(u => candidateIds.Contains(u.Id) && u.ApprovalStatus == "Approved" && u.Status == "Active")
            .ToListAsync(ct);

        int warnings = 0, restrictions = 0;
        foreach (var user in candidates)
        {
            var links = await AccountLinking.FindLinksAsync(db, user, encryption, ct);
            var risk = AccountLinking.Assess(links, AccountLinking.OwnSignals(user, encryption));
            // Act only when it got worse than what was last assessed: an admin who lifted a restriction
            // is not overruled every 10 minutes by the same evidence.
            if (risk.Score < RiskAssessment.ReviewThreshold || risk.Score <= (user.RiskScore ?? 0)) continue;
            user.RiskScore = risk.Score;
            user.RiskCheckedAt = now;

            string level;
            if (risk.Score >= RiskAssessment.RejectThreshold)
            {
                user.ProductBanUntil = Later(user.ProductBanUntil, now + LinkRestriction);
                user.PayoutHoldUntil = Later(user.PayoutHoldUntil, now + LinkPayoutHold);
                level = "RestrictListings+HoldPayouts";
                restrictions++;
                Notify(db, user.Id, "Account restricted",
                    "Some features of your account are limited while our team reviews it. Contact support if you think this is a mistake.", now);
            }
            else
            {
                level = "Warning";
                warnings++;
                Notify(db, user.Id, "Account under review", "Our team is reviewing recent activity on your account.", now);
            }
            log.Add("AccountMonitorLink", "User", user.Id.ToString(), "PII", new
            {
                level, risk.Score, risk.Reasons, linkedUserIds = links.Select(l => l.UserId).Distinct(),
            });
        }
        return (warnings, restrictions);
    }

    /// Sudden bulk listing compared with the seller's own history: typical of a bought or taken-over account
    /// being used to dump fake items before it gets shut down.
    private static async Task<int> ListingSpikesAsync(IApplicationDbContext db, ISensitiveDataLogger log, DateTime now, CancellationToken ct)
    {
        DateTimeOffset since = now.AddDays(-1), baselineStart = now.AddDays(-31);
        var stats = await db.Products
            .Where(p => p.SellerId != null && p.CreatedAt >= baselineStart)
            .GroupBy(p => p.SellerId!.Value)
            .Select(g => new { SellerId = g.Key, Last24h = g.Count(p => p.CreatedAt >= since), Before = g.Count(p => p.CreatedAt < since) })
            .ToListAsync(ct);
        var spiking = stats
            .Where(s => s.Last24h >= SpikeMinimum && s.Last24h >= SpikeFactor * Math.Max(1.0, s.Before / 30.0))
            .ToDictionary(s => s.SellerId);
        if (spiking.Count == 0) return 0;

        var ids = spiking.Keys.ToList();
        var sellers = await db.Users
            .Where(u => ids.Contains(u.Id) && u.Status == "Active" && (u.ProductBanUntil == null || u.ProductBanUntil < now))
            .ToListAsync(ct);
        foreach (var seller in sellers)
        {
            seller.ProductBanUntil = now + SpikeRestriction;
            Notify(db, seller.Id, "Listing paused",
                "Unusually many new listings were created on your account in the last 24 hours. Listing is paused for a day.", now);
            log.Add("AccountMonitorSpike", "User", seller.Id.ToString(), "PII", new
            {
                level = "RestrictListings", spiking[seller.Id].Last24h, previous30Days = spiking[seller.Id].Before,
            });
        }
        return sellers.Count;
    }

    private static DateTime Later(DateTime? current, DateTime proposed) => current > proposed ? current.Value : proposed;

    private static void Notify(IApplicationDbContext db, int userId, string title, string content, DateTime now) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId, Title = title, Content = content, Type = "InApp", Status = "Sent", SentAt = now, CreatedAt = now,
        });
}
