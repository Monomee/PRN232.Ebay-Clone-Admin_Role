using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EbayClone.Web.Infrastructure;

public static class RateLimitingExtensions
{
    // Tên policy — dùng ở nhiều nơi nên đặt constant
    public const string StrictPolicy = "strict";
    public const string StandardPolicy = "standard";
    public const string AuthenticatedPolicy = "authenticated";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Mỗi policy chia partition riêng: một client spam không làm khóa login của mọi người.
            // ponytail: bộ đếm nằm trong RAM từng pod; chạy nhiều pod thì nginx (limit_req theo IP) là lớp chặn chung.

            // ── 1. STRICT — Chống brute-force login / 2FA ─────────────────
            // 5 request / 60 giây cho mỗi IP
            options.AddPolicy(StrictPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                $"ip:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromSeconds(60),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0 // không cho queue, reject ngay
                }));

            // ── 2. STANDARD — Giới hạn chung toàn API ────────────────────
            // 60 request / 60 giây theo IP
            options.AddPolicy(StandardPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                $"ip:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromSeconds(60),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                }));

            // ── 3. AUTHENTICATED — Giới hạn theo UserId ──────────────────
            // 200 request / 60 giây cho mỗi user đã đăng nhập (sliding window), chưa login thì theo IP
            options.AddPolicy(AuthenticatedPolicy, context => RateLimitPartition.GetSlidingWindowLimiter(
                GetPartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromSeconds(60),
                    SegmentsPerWindow = 6, // kiểm tra mỗi 10 giây
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));

            // ── Response khi bị chặn (429 Too Many Requests) ─────────────
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Header Retry-After: báo client biết bao giờ thử lại
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                context.HttpContext.Response.Headers["X-RateLimit-Source"] = "App-RateLimiter";

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error": "Too many requests. Please slow down and try again later.", "source": "Application Layer"}""",
                    cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// Lấy partition key:
    ///   - Nếu đã login → dùng UserId (giới hạn theo user)
    ///   - Nếu chưa login → dùng IP address (giới hạn theo IP)
    /// </summary>
    public static string GetPartitionKey(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        return $"ip:{context.Connection.RemoteIpAddress}";
    }
}
