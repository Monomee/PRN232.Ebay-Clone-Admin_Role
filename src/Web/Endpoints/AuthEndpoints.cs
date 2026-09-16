using System.Security.Claims;
using EbayClone.Application.Authentication.Commands.Enable2FA;
using EbayClone.Application.Authentication.Commands.Login;
using EbayClone.Application.Authentication.Commands.Verify2FA;
using EbayClone.Application.Authentication.Commands.Verify2FASetup;
using EbayClone.Web.Infrastructure;

namespace EbayClone.Web.Endpoints;

public class Auth : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        // 🔴 STRICT (5 req/min) — chống brute-force
        group.MapPost("login", Login)
             .RequireRateLimiting(RateLimitingExtensions.StrictPolicy);

        group.MapPost("verify-2fa", Verify2FA)
             .RequireRateLimiting(RateLimitingExtensions.StrictPolicy);

        // 🟡 STANDARD (60 req/min)
        group.MapPost("logout", Logout)
             .RequireRateLimiting(RateLimitingExtensions.StandardPolicy);

        group.MapGet("me", Me).RequireAuthorization()
             .RequireRateLimiting(RateLimitingExtensions.StandardPolicy);

        // 🟢 AUTHENTICATED (200 req/min theo UserId)
        group.MapPost("enable-2fa", Enable2FA).RequireAuthorization()
             .RequireRateLimiting(RateLimitingExtensions.AuthenticatedPolicy);

        group.MapPost("verify-2fa-setup", Verify2FASetup).RequireAuthorization()
             .RequireRateLimiting(RateLimitingExtensions.AuthenticatedPolicy);
    }

    public async Task<IResult> Login(ISender sender, LoginCommand command, HttpContext http)
    {
        var result = await sender.Send(command);

        if (!result.Success)
            return Results.Ok(result);

        if (result.RequireTwoFactor)
            return Results.Ok(result); // chưa set cookie, chờ 2FA

        // ✅ Set HttpOnly cookie
        http.Response.Cookies.Append("auth_token", result.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        result.Token = null; // ❌ không trả token về body
        return Results.Ok(result);
    }

    public async Task<IResult> Verify2FA(ISender sender, Verify2FACommand command, HttpContext http)
    {
        var result = await sender.Send(command);

        if (!result.Success)
            return Results.Ok(result);

        // ✅ Set cookie sau khi 2FA thành công
        http.Response.Cookies.Append("auth_token", result.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Results.Ok(result);
    }

    public IResult Logout(HttpContext http)
    {
        http.Response.Cookies.Delete("auth_token");
        return Results.Ok();
    }

    public IResult Me(HttpContext http)
    {
        var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = http.User.FindFirst(ClaimTypes.Role)?.Value;
        var twoFactorEnabled = http.User.FindFirst("twoFactorEnabled")?.Value == "true"; // ✅ thêm

        return Results.Ok(new { email, userId, role, twoFactorEnabled });
    }

    public async Task<IResult> Enable2FA(ISender sender, HttpContext http)
    {
        // Lấy email từ JWT claim, không cần client gửi lên
        var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return Results.Unauthorized();

        var qrBase64 = await sender.Send(new Enable2FACommand(email));

        return Results.Ok(new
        {
            success = true,
            qrCode = qrBase64,          // base64 PNG → hiển thị trực tiếp trên frontend
            message = "Scan QR code with your authenticator app"
        });
    }
    public async Task<IResult> Verify2FASetup(ISender sender, HttpContext http, Verify2FASetupRequest req)
    {
        var userId = int.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var isValid = await sender.Send(new Verify2FASetupCommand(userId, req.Code));

        if (!isValid)
            return Results.Ok(new { success = false, errorMessage = "Mã không hợp lệ" });

        return Results.Ok(new { success = true });
    }

    public record Verify2FASetupRequest(string Code);
}
