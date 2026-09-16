using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing pending users.");
            }

            // Chạy định kỳ mỗi 5 phút (Batch Processing)
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }

        _logger.LogInformation("AutoReviewUsersService stopping.");
    }

    private async Task ProcessPendingUsersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var pendingUsers = await context.Users
            .Where(u => u.ApprovalStatus == "PendingApproval")
            .ToListAsync(cancellationToken);

        if (!pendingUsers.Any()) return;

        _logger.LogInformation("Found {Count} pending users for review.", pendingUsers.Count);

        foreach (var pendingUser in pendingUsers)
        {
            _logger.LogInformation("Reviewing User: {Username} (Id: {Id})", pendingUser.Username, pendingUser.Id);

            // Check duplication
            var isDuplicate = await context.Users
                .AnyAsync(existing =>
                    existing.Id != pendingUser.Id &&
                    (
                        // Check CCCD
                        (pendingUser.CCCD != null && existing.CCCD == pendingUser.CCCD) ||
                        // Check IP + Coordinates (must match all for a likely clone)
                        (pendingUser.LastLoginIp != null && existing.LastLoginIp == pendingUser.LastLoginIp &&
                         pendingUser.Latitude != null && existing.Latitude == pendingUser.Latitude &&
                         pendingUser.Longitude != null && existing.Longitude == pendingUser.Longitude)
                    ), cancellationToken);

            if (isDuplicate)
            {
                _logger.LogWarning("Potential duplicate found for User {Id}. Rejecting.", pendingUser.Id);
                
                pendingUser.ApprovalStatus = "Rejected";
                pendingUser.Status = "Banned";
                pendingUser.BannedReason = "Hệ thống tự động từ chối: Phát hiện tài khoản trùng lặp (IP/Tọa độ/CCCD).";
                pendingUser.BannedAt = DateTime.UtcNow;

                context.Notifications.Add(new Notification
                {
                    UserId = pendingUser.Id,
                    Title = "Account Rejected",
                    Content = "Your account has been automatically rejected due to duplication rules.",
                    Type = "InApp",
                    Status = "Sent",
                    SentAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogInformation("No duplicates found for User {Id}. Approving.", pendingUser.Id);

                pendingUser.ApprovalStatus = "Approved";
                pendingUser.Status = "Active";
                pendingUser.ApprovedAt = DateTime.UtcNow;

                context.Notifications.Add(new Notification
                {
                    UserId = pendingUser.Id,
                    Title = "Account Approved",
                    Content = "Your account has been automatically approved.",
                    Type = "InApp",
                    Status = "Sent",
                    SentAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
