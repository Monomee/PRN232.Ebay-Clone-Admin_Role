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
            // ── 1. STRICT — Chống brute-force login / 2FA ─────────────────
            // Chỉ cho phép 5 request / 60 giây từ cùng 1 IP
            options.AddFixedWindowLimiter(StrictPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromSeconds(60);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // không cho queue, reject ngay
            });

            // ── 2. STANDARD — Giới hạn chung toàn API ────────────────────
            // 60 request / 60 giây theo IP
            options.AddFixedWindowLimiter(StandardPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 60;
                limiterOptions.Window = TimeSpan.FromSeconds(60);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 5;
            });

            // ── 3. AUTHENTICATED — Giới hạn theo UserId ──────────────────
            // 200 request / 60 giây cho user đã đăng nhập (dùng sliding window)
            options.AddSlidingWindowLimiter(AuthenticatedPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 200;
                limiterOptions.Window = TimeSpan.FromSeconds(60);
                limiterOptions.SegmentsPerWindow = 6; // kiểm tra mỗi 10 giây
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 10;
            });

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

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error": "Too many requests. Please slow down and try again later."}""",
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
