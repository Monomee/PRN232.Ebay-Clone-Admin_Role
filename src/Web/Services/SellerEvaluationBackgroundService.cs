using EbayClone.Application.Sellers.Commands.EvaluateSellerLevels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Web.Services;

public class SellerEvaluationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SellerEvaluationBackgroundService> _logger;

    public SellerEvaluationBackgroundService(IServiceProvider serviceProvider, ILogger<SellerEvaluationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SellerEvaluationBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<EbayClone.Application.Common.Interfaces.IApplicationDbContext>();
                
                var criteria = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    context.SellerLevelCriteria, c => c.Id == 1, stoppingToken);
                
                bool force = bool.TryParse(Environment.GetEnvironmentVariable("FORCE_EVALUATION"), out bool f) && f;

                if (criteria != null && (DateTime.UtcNow >= criteria.NextEvaluationDate || force))
                {
                    _logger.LogInformation("Evaluation scheduled time reached (or forced). Running evaluation...");
                    var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                    var updated = await mediator.Send(new EvaluateSellerLevelsCommand(), stoppingToken);
                    
                    _logger.LogInformation("Evaluated seller levels. Updated {Count} sellers.", updated);

                    // Set next evaluation date to the 20th of the next month
                    var now = DateTime.UtcNow;
                    var nextYear = now.Year;
                    var nextMonth = now.Month;
                    if (now.Day >= 20 || force) {
                        nextMonth++;
                        if (nextMonth > 12) { nextMonth = 1; nextYear++; }
                    }
                    criteria.NextEvaluationDate = new DateTime(nextYear, nextMonth, 20, 0, 0, 0, DateTimeKind.Utc);
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in SellerEvaluationBackgroundService.");
            }

            // Polling every 15 seconds to allow UI-driven demo adjustments
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
