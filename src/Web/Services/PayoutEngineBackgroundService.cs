using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Payouts.Commands.RunPayoutEngine;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Web.Services;

/// <summary>
/// Background service that fires the Payout Engine on a schedule.
/// The schedule is read from PayoutConfig (ScheduledHourUtc, Frequency).
/// Can also be triggered immediately via the Admin "Force Run" API endpoint,
/// or rescheduled when PayoutConfig is updated.
/// </summary>
public class PayoutEngineBackgroundService : BackgroundService
{
    private static CancellationTokenSource? _rescheduleCts;
    private static readonly object _syncLock = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PayoutEngineBackgroundService> _logger;

    public PayoutEngineBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PayoutEngineBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public static void TriggerReschedule()
    {
        lock (_syncLock)
        {
            if (_rescheduleCts != null && !_rescheduleCts.IsCancellationRequested)
            {
                _rescheduleCts.Cancel();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PayoutEngineBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int scheduledHour = 2;
                string frequency = "Daily";

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    var config = await db.PayoutConfigs.AsNoTracking().FirstOrDefaultAsync(stoppingToken);
                    scheduledHour = Math.Clamp(config?.ScheduledHourUtc ?? 2, 0, 23);
                    frequency = config?.Frequency ?? "Daily";
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PayoutEngine: Failed to read PayoutConfig, falling back to defaults.");
                }

                var now = DateTime.UtcNow;
                var nextRun = new DateTime(now.Year, now.Month, now.Day, scheduledHour, 0, 0, DateTimeKind.Utc);

                if (now >= nextRun)
                {
                    if (string.Equals(frequency, "Weekly", StringComparison.OrdinalIgnoreCase))
                        nextRun = nextRun.AddDays(7);
                    else if (string.Equals(frequency, "Monthly", StringComparison.OrdinalIgnoreCase))
                        nextRun = nextRun.AddMonths(1);
                    else
                        nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;
                _logger.LogInformation("PayoutEngine: Next scheduled run in {Delay:hh\\:mm\\:ss} at {NextRun:o} (Frequency={Frequency}, HourUtc={Hour})",
                    delay, nextRun, frequency, scheduledHour);

                bool rescheduled = false;
                lock (_syncLock)
                {
                    _rescheduleCts?.Dispose();
                    _rescheduleCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                }

                try
                {
                    await Task.Delay(delay, _rescheduleCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    rescheduled = true;
                }

                if (rescheduled)
                {
                    continue;
                }

                if (stoppingToken.IsCancellationRequested) break;

                // Execute the payout engine
                using (var scope = _serviceProvider.CreateScope())
                {
                    var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                    var result = await mediator.Send(new RunPayoutEngineCommand(), stoppingToken);

                    _logger.LogInformation(
                        "PayoutEngine scheduled run complete. Success={S}, Failed={F}, Hold={H}, Skipped={K}, Disbursed=${D:F2}",
                        result.Success, result.Failed, result.OnHold, result.Skipped, result.TotalDisbursed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayoutEngine scheduled run encountered an unhandled error. Retrying in 1 hour.");
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("PayoutEngineBackgroundService stopped.");
    }
}
