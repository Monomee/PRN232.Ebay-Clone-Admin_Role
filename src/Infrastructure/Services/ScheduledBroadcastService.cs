using EbayClone.Application.Broadcasts.Commands.SendBroadcast;
using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;

/// Delivers broadcasts created with ScheduleAt once their time has come: Status Scheduled → Sent.
public class ScheduledBroadcastService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledBroadcastService> _logger;

    public ScheduledBroadcastService(IServiceProvider serviceProvider, ILogger<ScheduledBroadcastService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await SendDueAsync(
                    scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                    scope.ServiceProvider.GetRequiredService<INotificationNotifier>(),
                    scope.ServiceProvider.GetRequiredService<IEmailService>(),
                    _logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending scheduled broadcasts.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public static async Task<int> SendDueAsync(IApplicationDbContext context, INotificationNotifier notifier,
        IEmailService emailService, ILogger logger, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var due = await context.Notifications
            .Where(n => n.Status == "Scheduled" && n.ScheduledAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var n in due)
        {
            n.Status = "Sent";
            n.SentAt = now;
        }

        // Mark first so a crash during delivery cannot send the same broadcast twice.
        await context.SaveChangesAsync(cancellationToken);

        foreach (var n in due)
        {
            if (n.Type.Equals("InApp", StringComparison.OrdinalIgnoreCase))
                await notifier.NotifyNewNotificationAsync(n.Id, n.Title, n.Content ?? "", n.UserRole, n.UserId, cancellationToken);
            else if (n.Type.Equals("Email", StringComparison.OrdinalIgnoreCase))
                await SendBroadcastCommandHandler.SendEmailsAsync(context, emailService, n.UserRole, n.Title, n.Content ?? "", logger, cancellationToken);
        }

        return due.Count;
    }
}
