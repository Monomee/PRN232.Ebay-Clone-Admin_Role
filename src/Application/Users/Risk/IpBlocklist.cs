using System.Net;
using System.Net.Sockets;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users.Risk;

/// Sign-up blocklist by network. Graduated on purpose: one IP is often thousands of people (4G CGNAT, campus Wi-Fi)
/// and a cheater changes IP in seconds, so:
///   1 banned account on the IP  -> risk score only (AccountLinking: Ip link to a banned account = manual review)
///   AutoBlockBannedAccounts banned accounts within Evidence, or a datacenter IP with a banned account
///                               -> sign-up from that network refused for AutoBlockFor
/// Only sign-up is refused; existing accounts on the same network keep signing in.
public static class IpBlocklist
{
    public const int AutoBlockBannedAccounts = 3;
    public static readonly TimeSpan Evidence = TimeSpan.FromDays(30);
    public static readonly TimeSpan AutoBlockFor = TimeSpan.FromDays(30);
    public const int MinIpv4Prefix = 16, MinIpv6Prefix = 32; // an admin typo must not block a whole country

    /// Block unit of one client address: IPv4 the address itself, IPv6 its /64 (one subscriber holds the whole /64).
    public static IPNetwork NetworkOf(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork) return new IPNetwork(address, 32);
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPNetwork(new IPAddress(bytes), 64);
    }

    /// Admin input: a plain IP (blocked like NetworkOf) or a CIDR range not wider than MinIpv4Prefix / MinIpv6Prefix.
    public static IPNetwork? ParseAdminInput(string? input)
    {
        input = input?.Trim();
        if (string.IsNullOrEmpty(input)) return null;
        if (!input.Contains('/')) return IPAddress.TryParse(input, out var address) ? NetworkOf(address) : null;
        if (!IPNetwork.TryParse(input, out var network)) return null;
        var min = network.BaseAddress.AddressFamily == AddressFamily.InterNetwork ? MinIpv4Prefix : MinIpv6Prefix;
        return network.PrefixLength >= min ? network : null;
    }

    /// The block refusing a sign-up from `ip`, or null.
    // ponytail: loads every active block per sign-up (hundreds at most); an indexed range table if it ever grows large
    public static async Task<IpBlock?> ActiveBlockAsync(IApplicationDbContext db, string? ip, DateTime now, CancellationToken ct)
    {
        if (!IPAddress.TryParse(ip?.Trim(), out var address)) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var active = await db.IpBlocks.Where(b => b.LiftedAt == null && b.ExpiresAt > now).ToListAsync(ct);
        return active.FirstOrDefault(b => IPNetwork.TryParse(b.Network, out var n) && n.Contains(address));
    }

    /// Adds automatic blocks from recent bans (runs with the account monitor). A network whose block was already
    /// decided on this evidence (still active, expired, or lifted by an admin) is not blocked again until a newer ban.
    public static async Task<int> AutoBlockAsync(IApplicationDbContext db, ISensitiveDataLogger log, DateTime now, CancellationToken ct)
    {
        DateTime? since = now - Evidence;
        var banned = await db.Users.Where(AccountLinking.BannedFilter).Where(u => u.BannedAt >= since)
            .Select(u => new { u.Id, u.BannedAt, u.LastLoginIp }).ToListAsync(ct);
        if (banned.Count == 0) return 0;
        var bannedAt = banned.ToDictionary(b => b.Id, b => b.BannedAt!.Value);
        var ids = bannedAt.Keys.ToList();
        var signIns = await db.LoginHistories.Where(h => ids.Contains(h.UserId) && h.IpAddress != null)
            .Select(h => new { h.UserId, h.IpAddress }).Distinct().ToListAsync(ct);

        // Sign-up IP (LastLoginIp until the first sign-in) plus every sign-in IP; private ones are our own proxies.
        var byNetwork = banned.Select(b => (b.Id, Ip: b.LastLoginIp))
            .Concat(signIns.Select(s => (Id: s.UserId, Ip: s.IpAddress)))
            .Select(e => (e.Id, Ip: AccountLinking.NormalizeIp(e.Ip)))
            .Where(e => e.Ip != null)
            .GroupBy(e => NetworkOf(IPAddress.Parse(e.Ip!)).ToString(), e => e.Id);

        var added = 0;
        foreach (var group in byNetwork)
        {
            var users = group.Distinct().ToList();
            var datacenter = IpIntelligence.DatacenterProvider(IPNetwork.Parse(group.Key).BaseAddress.ToString());
            if (users.Count < AutoBlockBannedAccounts && datacenter == null) continue;

            var newestBan = users.Max(id => bannedAt[id]);
            if (await db.IpBlocks.AnyAsync(b => b.Network == group.Key && b.CreatedAt >= newestBan, ct)) continue;

            db.IpBlocks.Add(new IpBlock
            {
                Network = group.Key, IsAutomatic = true, CreatedAt = now, ExpiresAt = now + AutoBlockFor,
                Reason = datacenter != null
                    ? $"Tự động: IP datacenter/VPS ({datacenter}) có {users.Count} tài khoản bị khóa/từ chối trong {Evidence.TotalDays:0} ngày"
                    : $"Tự động: {users.Count} tài khoản bị khóa/từ chối trong {Evidence.TotalDays:0} ngày",
            });
            log.Add("IpBlockAuto", "IpBlock", group.Key, "PII", new { linkedUserIds = users, datacenter });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }
}
