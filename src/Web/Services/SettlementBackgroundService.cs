using EbayClone.Application.Financials.Commands.SettlePendingFunds;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Web.Services;

public class SettlementBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SettlementBackgroundService> _logger;

    public SettlementBackgroundService(IServiceProvider serviceProvider, ILogger<SettlementBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SettlementBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                
                var settledCount = await mediator.Send(new SettlePendingFundsCommand(), stoppingToken);
                
                if (settledCount > 0)
                {
                    _logger.LogInformation("Auto-settled funds for {Count} orders.", settledCount);
                }
                
                // Run settlement checks every 15 seconds (reduced for demo UI accuracy)
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Khi stoppingToken bị hủy, Task.Delay sẽ quăng exception này.
                // Catch ở đây để cho phép vòng lặp thoát một cách êm ái.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing SettlePendingFundsCommand.");
            }
        }
        
        _logger.LogInformation("SettlementBackgroundService is stopping.");
    }
}
