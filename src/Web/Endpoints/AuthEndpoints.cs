using System.Security.Claims;
using EbayClone.Application.Authentication.Commands.Enable2FA;
using EbayClone.Application.Authentication.Commands.Login;
using EbayClone.Application.Authentication.Commands.Verify2FA;
using EbayClone.Application.Authentication.Commands.Verify2FASetup;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Services;
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

    private const string TwoFactorChallengeCookie = "2fa_challenge";

    public async Task<IResult> Login(ISender sender, IJwtService jwt, LoginCommand command, HttpContext http)
    {
        var result = await sender.Send(command);

        if (!result.Success)
            return Results.Ok(result);

        if (result.RequireTwoFactor)
        {
            // Chưa set auth cookie. Chỉ cấp challenge chứng minh đã qua bước mật khẩu, verify-2fa đọc userId từ đây.
            http.Response.Cookies.Append(TwoFactorChallengeCookie, jwt.GenerateTwoFactorChallenge(result.UserId), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/Auth",
                Expires = DateTimeOffset.UtcNow.Add(JwtService.TwoFactorChallengeLifetime)
            });
            return Results.Ok(result);
        }

        // ✅ Set HttpOnly cookie
        http.Response.Cookies.Append("auth_token", result.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.Add(JwtService.TokenLifetime) // cookie dies with the JWT inside it
        });

        result.Token = null; // ❌ không trả token về body
        return Results.Ok(result);
    }

    public async Task<IResult> Verify2FA(ISender sender, IJwtService jwt, SecurityCache cache, Verify2FARequest req, HttpContext http)
    {
        // userId trong body (nếu client cũ còn gửi) bị bỏ qua: chỉ tin challenge do bước mật khẩu cấp
        var userId = jwt.ValidateTwoFactorChallenge(http.Request.Cookies[TwoFactorChallengeCookie]);
        if (userId == null)
            return Results.Ok(new Verify2FAResponse { Success = false, ErrorMessage = "Phiên xác thực đã hết hạn, vui lòng đăng nhập lại" });

        var result = await sender.Send(new Verify2FACommand(userId.Value, req.Code));

        if (!result.Success)
            return Results.Ok(result);

        // Một mã TOTP chỉ dùng được một lần: người nhìn thấy mã vừa nhập không thể đăng nhập lại trong cùng 30 giây.
        if (!await cache.TryUseOnceAsync($"totp:{userId}:{result.TimeStep}", TimeSpan.FromMinutes(2)))
            return Results.Ok(new Verify2FAResponse { Success = false, ErrorMessage = "Mã 2FA này đã được sử dụng, vui lòng đợi mã mới" });

        http.Response.Cookies.Delete(TwoFactorChallengeCookie, new CookieOptions { Path = "/api/Auth" });

        // ✅ Set cookie sau khi 2FA thành công
        http.Response.Cookies.Append("auth_token", result.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.Add(JwtService.TokenLifetime) // cookie dies with the JWT inside it
        });

        result.Token = null; // ❌ không trả token về body (giống login): JS không được đọc JWT
        return Results.Ok(result);
    }

    public async Task<IResult> Logout(HttpContext http, SecurityCache revoked)
    {
        // Deleting the cookie is not enough: a copied token would stay valid until it expires.
        var jti = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        if (jti != null && long.TryParse(http.User.FindFirst("exp")?.Value, out var exp))
            await revoked.RevokeAsync(jti, DateTimeOffset.FromUnixTimeSeconds(exp));

        http.Response.Cookies.Delete("auth_token");
        return Results.Ok();
    }

    public IResult Me(HttpContext http)
    {
        var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = http.User.FindFirst(ClaimTypes.Role)?.Value;
        var twoFactorEnabled = http.User.FindFirst("twoFactorEnabled")?.Value == "true"; // ✅ thêm

        // Custom admin roles are authorized by these (see AdminPermissions); the UI uses them for routes and menu
        var permissions = http.User.FindAll(AdminPermissions.ClaimType).Select(c => c.Value).ToList();

        return Results.Ok(new { email, userId, role, twoFactorEnabled, permissions });
    }

    public async Task<IResult> Enable2FA(ISender sender, HttpContext http)
    {
        // Lấy email từ JWT claim, không cần client gửi lên
        var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return Results.Unauthorized();

        var qrBase64 = await sender.Send(new Enable2FACommand(email));
        if (qrBase64 == null)
            return Results.Ok(new { success = false, message = "2FA đã được bật cho tài khoản này" });

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
    public record Verify2FARequest(string Code);
}
