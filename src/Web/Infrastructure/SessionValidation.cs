using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EbayClone.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace EbayClone.Web.Infrastructure;

/// A signed JWT alone is not enough: the account must still be allowed in, with the role the token was issued for.
/// Bans, rejections, role changes and logout therefore take effect on the next request instead of after token expiry.
public static class SessionValidation
{
    // Before 2FA is active a session may only finish the 2FA setup (and see who it is / log out).
    private static readonly string[] AllowedWithout2FA =
        ["/api/Auth/me", "/api/Auth/logout", "/api/Auth/enable-2fa", "/api/Auth/verify-2fa-setup"];

    public static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var principal = ctx.Principal!;
        var services = ctx.HttpContext.RequestServices;

        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti != null && await services.GetRequiredService<RevokedTokenStore>().IsRevokedAsync(jti))
        {
            ctx.Fail("Token was revoked (logged out).");
            return;
        }

        // ponytail: one PK lookup per authenticated request; add a short cache if it ever shows up in profiling
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            ctx.Fail("Token has no user id.");
            return;
        }
        var user = await services.GetRequiredService<IApplicationDbContext>().Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.ApprovalStatus, u.Role, u.TwoFactorEnabled })
            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

        if (user == null || user.Status == "Banned" || user.ApprovalStatus == "Rejected")
        {
            ctx.Fail("Account no longer allowed to sign in.");
            return;
        }

        // Role changed since the token was issued: its role/permission claims are stale, sign in again.
        if (!string.Equals(user.Role, principal.FindFirstValue(ClaimTypes.Role), StringComparison.Ordinal))
        {
            ctx.Fail("Role changed since sign-in.");
            return;
        }

        if (!user.TwoFactorEnabled && !AllowedWithout2FA.Any(p => ctx.HttpContext.Request.Path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            ctx.Fail("Two-factor authentication must be enabled first.");
    }
}

/// Logged-out token ids until they expire. Shared through Redis when configured (several pods), in memory otherwise.
public sealed class RevokedTokenStore
{
    private readonly IMemoryCache _memory;
    private readonly Lazy<IDatabase>? _redis;

    public RevokedTokenStore(IConfiguration configuration, IMemoryCache memory)
    {
        _memory = memory;
        var redis = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false;
            _redis = new Lazy<IDatabase>(() => ConnectionMultiplexer.Connect(options).GetDatabase());
        }
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt)
    {
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        if (_redis != null) await _redis.Value.StringSetAsync(Key(jti), 1, ttl);
        else _memory.Set(Key(jti), true, ttl);
    }

    public async Task<bool> IsRevokedAsync(string jti) =>
        _redis != null ? await _redis.Value.KeyExistsAsync(Key(jti)) : _memory.TryGetValue(Key(jti), out _);

    private static string Key(string jti) => "ebay-revoked-jwt:" + jti;
}
