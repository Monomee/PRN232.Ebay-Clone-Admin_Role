namespace EbayClone.Web.Infrastructure;

/// <summary>
/// Middleware thêm các security header theo khuyến nghị OWASP.
/// Khắc phục các lỗi Nikto: missing CSP, X-Content-Type-Options,
/// HSTS, Permissions-Policy, Referrer-Policy, và ẩn header Server.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // 1. Ngăn chặn MIME-sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // 2. Chống clickjacking
        headers["X-Frame-Options"] = "SAMEORIGIN";

        // 3. Kiểm soát thông tin Referrer
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // 4. Giới hạn quyền truy cập API browser
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        // 5. Bắt buộc HTTPS (1 năm, bao gồm subdomain)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // 6. Content Security Policy
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data: blob: https:; " +
            "connect-src 'self' wss: ws:; " +
            "frame-ancestors 'self';";

        // 7. Xóa header Server (ẩn công nghệ backend)
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await _next(context);
    }
}
