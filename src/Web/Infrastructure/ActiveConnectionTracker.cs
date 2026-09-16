using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using EbayClone.Web.Hubs;

namespace EbayClone.Web.Infrastructure;

public interface IActiveConnectionTracker
{
    void RecordActivity(string ipAddress);
    IEnumerable<ActiveConnection> GetActiveConnections(TimeSpan timeframe);
}

public class ActiveConnection
{
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public bool IsWhitelisted { get; set; }
}

public class ActiveConnectionTracker : IActiveConnectionTracker
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly string[] _allowedIps;
    
    // A thread-safe dictionary to maintain the IP addresses and latest timestamps
    private readonly ConcurrentDictionary<string, DateTime> _activeIps = new();

    public ActiveConnectionTracker(IHubContext<NotificationHub> hubContext, IConfiguration configuration)
    {
        _hubContext = hubContext;
        _configuration = configuration;
        _allowedIps = _configuration.GetSection("InternalIps").Get<string[]>() ?? new[] { "127.0.0.1", "::1" };
    }

    public void RecordActivity(string ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            _activeIps.AddOrUpdate(ipAddress, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);
            
            // Broadcast to Admin group
            _hubContext.Clients.Group("Admins").SendAsync("ConnectionUpdated", new { 
                IpAddress = ipAddress, 
                Timestamp = DateTime.UtcNow,
                ActiveCount = _activeIps.Count 
            });

            // Cleanup old records occasionally to avoid memory leaks
            if (_activeIps.Count % 100 == 0)
            {
                CleanupOldRecords();
            }
        }
    }

    public IEnumerable<ActiveConnection> GetActiveConnections(TimeSpan timeframe)
    {
        var cutoff = DateTime.UtcNow.Subtract(timeframe);
        return _activeIps
            .Where(x => x.Value >= cutoff)
            .Select(x => new ActiveConnection 
            { 
                IpAddress = x.Key, 
                LastSeen = x.Value,
                IsWhitelisted = IsIpWhitelisted(x.Key)
            })
            .OrderByDescending(x => x.LastSeen);
    }

    private bool IsIpWhitelisted(string ipAddress)
    {
        foreach (var ip in _allowedIps)
        {
            if (ipAddress == ip || (ip.EndsWith(".*") && ipAddress.StartsWith(ip.Replace(".*", ""))))
            {
                return true;
            }
        }
        return false;
    }
    
    private void CleanupOldRecords()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1); // Keep connections from the last 1 hour max in memory.
        var keysToRemove = _activeIps.Where(x => x.Value < cutoff).Select(x => x.Key).ToList();
        
        foreach(var key in keysToRemove)
        {
            _activeIps.TryRemove(key, out _);
        }
    }
}
