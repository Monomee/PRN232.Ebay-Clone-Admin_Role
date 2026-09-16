using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace EbayClone.Web.Infrastructure;

public class InternalIpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InternalIpMiddleware> _logger;
    private readonly string[] _allowedIps;

    public InternalIpMiddleware(RequestDelegate next, ILogger<InternalIpMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        
        // Retrieve allowed IPs from appsettings.json. If missing, default to localhost.
        _allowedIps = configuration.GetSection("InternalIps").Get<string[]>() ?? new[] { "127.0.0.1", "::1" };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;

        if (remoteIp == null)
        {
            _logger.LogWarning("Forbidden Request - Remote IP is null");
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        // If IPv4 mapped to IPv6, map back to IPv4 for comparison
        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        bool isAllowed = false;
        var remoteIpString = remoteIp.ToString();
        var path = context.Request.Path.Value ?? "";

        // Chỉ log IP cho các request API thực tế, bỏ qua log cho SignalR (vì nó spam rất nhiều)
        if (!path.StartsWith("/hubs/"))
        {
            _logger.LogInformation("Incoming request from IP: {RemoteIp} to {Path}", remoteIpString, path);
        }

        /* Tắt log header để tránh spam console
        foreach (var header in context.Request.Headers)
        {
            _logger.LogInformation("Header: {Key} = {Value}", header.Key, header.Value);
        }
        */

        // Tracker the connection
        // TRÁNH INFINITE LOOP: Không ghi nhận activity cho chính request lấy danh sách IP
        if (!path.Equals("/api/SystemAdmin/active-ips", StringComparison.OrdinalIgnoreCase))
        {
            var tracker = context.RequestServices.GetService<IActiveConnectionTracker>();
            tracker?.RecordActivity(remoteIpString);
        }

        // Check against exact matches or subnets (simple prefix check for this example)
        foreach (var ip in _allowedIps)
        {
            if (remoteIpString == ip || (ip.EndsWith(".*") && remoteIpString.StartsWith(ip.Replace(".*", ""))))
            {
                isAllowed = true;
                break;
            }
        }

        if (!isAllowed)
        {
            _logger.LogWarning("Forbidden Request from Unauthorized IP: {RemoteIp}", remoteIpString);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsync("Access denied. Unauthorized IP.");
            return;
        }

        await _next(context);
    }
}
