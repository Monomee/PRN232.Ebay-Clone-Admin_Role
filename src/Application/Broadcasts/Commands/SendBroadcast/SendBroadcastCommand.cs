using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EbayClone.Application.Broadcasts.Commands.SendBroadcast;

public record SendBroadcastCommand : IRequest<int>
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string TargetAudience { get; init; } = "All"; // All, Seller, Buyer, Group
    public string? TargetGroup { get; init; } // Used when TargetAudience = Group
    public List<string> Channels { get; init; } = new() { "InApp" }; // Email, InApp
    public DateTime? ScheduleAt { get; init; }
    public int CreatedBy { get; init; }
}

public class SendBroadcastCommandHandler : IRequestHandler<SendBroadcastCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationNotifier _notificationNotifier;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SendBroadcastCommandHandler> _logger;

    public SendBroadcastCommandHandler(
        IApplicationDbContext context, 
        INotificationNotifier notificationNotifier,
        IServiceScopeFactory scopeFactory,
        ILogger<SendBroadcastCommandHandler> logger)
    {
        _context = context;
        _notificationNotifier = notificationNotifier;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<int> Handle(SendBroadcastCommand request, CancellationToken cancellationToken)
    {
        var validChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Email", "InApp" };
        if (request.Channels.Count == 0 || request.Channels.Any(c => !validChannels.Contains(c)))
        {
            throw new ArgumentException("Invalid channel. Valid values: Email, InApp");
        }

        var validAudiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "All", "Seller", "Buyer", "Group" };
        if (!validAudiences.Contains(request.TargetAudience))
        {
            throw new ArgumentException("Invalid target audience. Valid values: All, Seller, Buyer, Group");
        }

        if (string.Equals(request.TargetAudience, "Group", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.TargetGroup))
        {
            throw new ArgumentException("TargetGroup is required when TargetAudience is Group.");
        }

        var now = DateTime.UtcNow;
        var targetRole = string.Equals(request.TargetAudience, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : string.Equals(request.TargetAudience, "Group", StringComparison.OrdinalIgnoreCase)
                ? $"Group:{request.TargetGroup}"
                : request.TargetAudience;

        // Create notification for each channel
        foreach (var channel in request.Channels)
        {
            var notification = new Notification
            {
                UserId = null, // Broadcast to all matching users
                UserRole = targetRole,
                Title = request.Title,
                Content = request.Content,
                Type = channel,
                Status = request.ScheduleAt.HasValue ? "Scheduled" : "Sent",
                ScheduledAt = request.ScheduleAt,
                SentAt = request.ScheduleAt.HasValue ? null : now,
                CreatedBy = request.CreatedBy,
                CreatedAt = now
            };

            _context.Notifications.Add(notification);
        }

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = request.CreatedBy,
            Action = request.ScheduleAt.HasValue ? "ScheduleBroadcast" : "SendBroadcast",
            TargetType = "Broadcast",
            Details = JsonSerializer.Serialize(new
            {
                after = new
                {
                    request.Title,
                    request.Content,
                    request.TargetAudience,
                    request.TargetGroup,
                    request.Channels,
                    request.ScheduleAt
                }
            }),
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        // Notify via SignalR for InApp notifications that are NOT scheduled (sent immediately)
        if (!request.ScheduleAt.HasValue && request.Channels.Contains("InApp", StringComparer.OrdinalIgnoreCase))
        {
            // We need to find the ID of the InApp notification we just added
            // Since we just saved, the IDs should be populated.
            // (Note: this assumes only one InApp notification was added in this loop, which is true by logic)
            var inAppNotification = _context.Notifications.Local
                .FirstOrDefault(n => n.Type.Equals("InApp", StringComparison.OrdinalIgnoreCase) && n.Title == request.Title && n.CreatedAt == now);
            
            if (inAppNotification != null)
            {
                await _notificationNotifier.NotifyNewNotificationAsync(
                    inAppNotification.Id,
                    inAppNotification.Title,
                    inAppNotification.Content ?? "",
                    inAppNotification.UserRole,
                    inAppNotification.UserId,
                    cancellationToken);
            }
        }

        if (!request.ScheduleAt.HasValue && request.Channels.Contains("Email", StringComparer.OrdinalIgnoreCase))
        {
            var targetAudience = request.TargetAudience;
            var targetGroup = request.TargetGroup;
            var title = request.Title;
            var content = request.Content;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    _logger.LogInformation("Starting background email broadcast...");

                    IQueryable<User> query = dbContext.Users.Where(u => !string.IsNullOrEmpty(u.Email));

                    if (string.Equals(targetAudience, "Seller", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(u => u.Role == "Seller");
                    }
                    else if (string.Equals(targetAudience, "Buyer", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(u => u.Role == "Buyer");
                    }
                    else if (string.Equals(targetAudience, "Group", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(targetGroup))
                    {
                        query = query.Where(u => u.Role == targetGroup); 
                    }

                    var emails = await query.Select(u => u.Email).ToListAsync();
                    _logger.LogInformation("Found {Count} users to email for broadcast '{Title}'", emails.Count, title);

                    int successCount = 0;
                    foreach (var email in emails)
                    {
                        if (string.IsNullOrEmpty(email)) continue;
                        try
                        {
                            await emailService.SendEmailAsync(email, title, content);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send email broadcast to {Email}", email);
                        }
                    }

                    _logger.LogInformation("Finished email broadcast '{Title}'. Successfully sent {SuccessCount}/{Total} emails.", title, successCount, emails.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error during background email broadcast.");
                }
            });
        }

        return request.Channels.Count; // Return number of notifications created
    }
}
