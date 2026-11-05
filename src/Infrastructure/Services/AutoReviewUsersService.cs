using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Application.Users.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;

public class AutoReviewUsersService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoReviewUsersService> _logger;

    public AutoReviewUsersService(IServiceProvider serviceProvider, ILogger<AutoReviewUsersService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoReviewUsersService starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingUsersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing pending users.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("AutoReviewUsersService stopping.");
    }

    // Linking data of everyone (not only the pending users) must be current, or a returning banned seller is missed.
    private DateTime _nextFullSync = DateTime.MinValue;
    private static readonly TimeSpan FullSyncInterval = TimeSpan.FromMinutes(10);

    private async Task ProcessPendingUsersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var sensitiveLog = scope.ServiceProvider.GetRequiredService<ISensitiveDataLogger>();

        // Every API replica runs this service; only one may review / monitor at a time, or users get decided and
        // notified twice. No guard registered (tests, single process): run.
        // ponytail: a lease without renewal; a run longer than its ttl may overlap the next one (the unique index on
        // UserIdentifiers keeps that harmless). Renew the lease if runs ever get that long.
        var guard = scope.ServiceProvider.GetService<IRunOnceGuard>();
        async Task<bool> Lead(string job, TimeSpan ttl) => guard == null || await guard.TryUseOnceAsync("job:" + job, ttl);

        if (DateTime.UtcNow >= _nextFullSync && await Lead("account-monitor", FullSyncInterval - TimeSpan.FromSeconds(30)))
        {
            await AccountLinking.SyncIdentifiersAsync(context, encryption, context.Users, cancellationToken);
            _nextFullSync = DateTime.UtcNow + FullSyncInterval;
            // Own scope/DbContext: a monitoring failure must not stop new accounts from being reviewed below.
            try
            {
                using var monitorScope = _serviceProvider.CreateScope();
                var sp = monitorScope.ServiceProvider;
                var monitorDb = sp.GetRequiredService<IApplicationDbContext>();
                var monitor = await AccountMonitor.RunAsync(monitorDb, encryption,
                    sp.GetRequiredService<ISensitiveDataLogger>(), DateTime.UtcNow, cancellationToken);
                if (monitor != new MonitorResult(0, 0, 0)) _logger.LogWarning("Account monitor: {Result}", monitor);
                var purged = await SignInMonitor.PurgeOldAsync(monitorDb, DateTime.UtcNow, cancellationToken);
                if (purged > 0) _logger.LogInformation("Deleted {Count} sign-in records older than {Days} days.", purged, SignInMonitor.Retention.TotalDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Account monitor failed.");
            }
        }

        // RiskCheckedAt == null: a user left pending for manual review is not re-scored every tick.
        var pending = context.Users.Where(u => u.ApprovalStatus == "PendingApproval" && u.RiskCheckedAt == null);
        if (!await pending.AnyAsync(cancellationToken) || !await Lead("auto-review", TimeSpan.FromSeconds(14))) return;
        await AccountLinking.SyncIdentifiersAsync(context, encryption, pending, cancellationToken);
        var pendingUsers = await pending.OrderBy(u => u.Id).ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} pending users for review.", pendingUsers.Count);

        foreach (var pendingUser in pendingUsers)
        {
            var links = await AccountLinking.FindLinksAsync(context, pendingUser, encryption, cancellationToken);
            var risk = AccountLinking.Assess(links, AccountLinking.OwnSignals(pendingUser, encryption));
            pendingUser.RiskScore = risk.Score;
            pendingUser.RiskCheckedAt = DateTime.UtcNow;
            _logger.LogInformation("Risk check User {Id}: score {Score} -> {Decision}", pendingUser.Id, risk.Score, risk.Decision);

            switch (risk.Decision)
            {
                case "Reject":
                    pendingUser.ApprovalStatus = "Rejected";
                    pendingUser.Status = "Banned";
                    // Generic on purpose: the user must not learn which account or field gave them away.
                    pendingUser.BannedReason = $"Hệ thống tự động từ chối: thông tin định danh trùng với tài khoản khác (điểm rủi ro {risk.Score}).";
                    pendingUser.BannedAt = DateTime.UtcNow;
                    Notify(context, pendingUser.Id, "Account Rejected", "Your account has been automatically rejected due to duplication rules.");
                    break;
                case "Review":
                    // Stays PendingApproval: it shows up in the admin's pending tab with its score and linked accounts.
                    Notify(context, pendingUser.Id, "Account Under Review", "Your account is being reviewed by our team.");
                    break;
                default:
                    pendingUser.ApprovalStatus = "Approved";
                    pendingUser.Status = "Active";
                    pendingUser.ApprovedAt = DateTime.UtcNow;
                    Notify(context, pendingUser.Id, "Account Approved", "Your account has been automatically approved.");
                    break;
            }

            sensitiveLog.Add("AutoReviewUser", "User", pendingUser.Id.ToString(), "PII", new
            {
                pendingUser.ApprovalStatus, pendingUser.Status, risk.Score, risk.Decision, risk.Reasons,
                linkedUserIds = links.Select(l => l.UserId).Distinct(),
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void Notify(IApplicationDbContext context, int userId, string title, string content) =>
        context.Notifications.Add(new Notification
        {
            UserId = userId, Title = title, Content = content, Type = "InApp", Status = "Sent",
            SentAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
        });
}
