using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;

public class ReturnRequestEscalationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReturnRequestEscalationService> _logger;

    public ReturnRequestEscalationService(IServiceProvider serviceProvider, ILogger<ReturnRequestEscalationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReturnRequestEscalationService starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EscalateRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while escalating return requests.");
            }

            // Chạy định kỳ mỗi 1 giờ
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("ReturnRequestEscalationService stopping.");
    }

    private async Task EscalateRequestsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationNotifier>();

        var escalationThreshold = DateTime.UtcNow.AddDays(-3);

        var pendingRequests = await context.ReturnRequests
            .Where(r => r.Status == ReturnStatuses.Pending && r.CreatedAt < escalationThreshold)
            .ToListAsync(cancellationToken);

        if (!pendingRequests.Any()) return;

        _logger.LogInformation("Found {Count} requests for auto-escalation.", pendingRequests.Count);

        foreach (var request in pendingRequests)
        {
            _logger.LogInformation("Escalating Return Request #{Id} (Created: {CreatedAt})", request.Id, request.CreatedAt);

            request.Status = ReturnStatuses.Escalated;
            
            // Broadcast the update
            await notifier.NotifyReturnRequestUpdatedAsync(request.Id, request.Status, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
