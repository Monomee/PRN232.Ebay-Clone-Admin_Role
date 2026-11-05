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

        // Every token we issue carries a jti; one without it could never be revoked by logout.
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti == null)
        {
            ctx.Fail("Token has no id (issued before logout revocation existed): sign in again.");
            return;
        }
        if (await services.GetRequiredService<SecurityCache>().IsRevokedAsync(jti))
        {
            ctx.Fail("Token was revoked (logged out).");
            return;
        }

        // ponytail: two indexed lookups (user, admin role) per authenticated request; add a short cache if it ever shows up in profiling
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            ctx.Fail("Token has no user id.");
            return;
        }
        var db = services.GetRequiredService<IApplicationDbContext>();
        var user = await db.Users
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

        // Same for a custom admin role whose permissions were edited or unassigned since sign-in.
        var granted = await db.GetAdminPermissionsAsync(userId, ctx.HttpContext.RequestAborted);
        var inToken = principal.FindAll(AdminPermissions.ClaimType).Select(c => c.Value);
        if (!granted.ToHashSet().SetEquals(inToken))
        {
            ctx.Fail("Permissions changed since sign-in.");
            return;
        }

        // Security:AllowApiWithout2FA is a load-test switch (JMeter accounts without 2FA); refused in Production at startup.
        if (!user.TwoFactorEnabled && !services.GetRequiredService<IConfiguration>().GetValue<bool>("Security:AllowApiWithout2FA")
            && !AllowedWithout2FA.Any(p => ctx.HttpContext.Request.Path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            ctx.Fail("Two-factor authentication must be enabled first.");
    }
}

/// Short-lived security state: logged-out token ids and already-used 2FA codes.
/// Shared through Redis when configured (several pods), in memory otherwise.
public sealed class SecurityCache
{
    private readonly IMemoryCache _memory;
    private readonly Lazy<IDatabase>? _redis;

    private static readonly object MemoryLock = new();

    public SecurityCache(IConfiguration configuration, IMemoryCache memory)
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

    /// True the first time a key is used within ttl, false on any repeat (atomic, also across pods with Redis).
    public async Task<bool> TryUseOnceAsync(string key, TimeSpan ttl)
    {
        key = "ebay-used-once:" + key;
        if (_redis != null) return await _redis.Value.StringSetAsync(key, 1, ttl, When.NotExists);
        lock (MemoryLock)
        {
            if (_memory.TryGetValue(key, out _)) return false;
            _memory.Set(key, true, ttl);
            return true;
        }
    }
}
