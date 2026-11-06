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

        // 5. HSTS: do app.UseHsts() lo (chỉ ngoài Development, tự bỏ qua localhost để không khóa HTTPS cho mọi project localhost)

        // 6. Content Security Policy
        // React app chỉ chạy script từ file của chính nó (build không có inline script / eval).
        // Riêng trang Swagger UI (/api/*.html) cần inline script nên được nới.
        var path = context.Request.Path.Value ?? "";
        var isSwaggerUi = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            // Cloudflare Turnstile (captcha on /register): its script and the iframe it renders.
            (isSwaggerUi ? "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " : "script-src 'self' https://challenges.cloudflare.com; ") +
            "frame-src https://challenges.cloudflare.com; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " + // bootstrap-icons (Ebay.css)
            "font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
            "img-src 'self' data: blob: https:; " +
            "connect-src 'self' wss: ws:; " +
            "object-src 'none'; base-uri 'self'; form-action 'self'; " +
            "frame-ancestors 'self';";

        // 7. Header Server bị tắt ở Kestrel (AddServerHeader = false trong Program.cs)

        await _next(context);
    }
}
