using EbayClone.Application.Payouts.Commands.RunPayoutEngine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Web.Services;

/// <summary>
/// Background service that fires the Payout Engine on a daily schedule.
/// The schedule is read from PayoutConfig (ScheduledHourUtc).
/// Can also be triggered immediately via the Admin "Force Run" API endpoint.
/// </summary>
public class PayoutEngineBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PayoutEngineBackgroundService> _logger;

    public PayoutEngineBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PayoutEngineBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PayoutEngineBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate delay until next scheduled run (default: 2:00 AM UTC daily)
                var now = DateTime.UtcNow;
                var nextRun = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0, DateTimeKind.Utc);

                // If already past the scheduled time today, schedule for tomorrow
                if (now >= nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("PayoutEngine: Next scheduled run in {Delay:hh\\:mm\\:ss} at {NextRun:o}",
                    delay, nextRun);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                // Execute the payout engine
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                var result = await mediator.Send(new RunPayoutEngineCommand(), stoppingToken);

                _logger.LogInformation(
                    "PayoutEngine scheduled run complete. Success={S}, Failed={F}, Hold={H}, Skipped={K}, Disbursed=${D:F2}",
                    result.Success, result.Failed, result.OnHold, result.Skipped, result.TotalDisbursed);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayoutEngine scheduled run encountered an unhandled error. Retrying in 1 hour.");
                // On error, wait 1 hour before retrying to avoid a tight retry loop
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("PayoutEngineBackgroundService stopped.");
    }
}
