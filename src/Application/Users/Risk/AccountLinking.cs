using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users.Risk;

public record LinkedAccount(int UserId, string Type, bool Banned);

/// Risk that comes from the account itself, not from other accounts.
public record RiskSignal(string Reason, int Weight);

public record RiskAssessment(int Score, IReadOnlyList<string> Reasons)
{
    public const int ReviewThreshold = 30;
    public const int RejectThreshold = 80;
    public string Decision => Score >= RejectThreshold ? "Reject" : Score >= ReviewThreshold ? "Review" : "Approve";
}

/// Links accounts that share identity data (the usual way a banned seller comes back under a new account)
/// and scores how likely a new account belongs to someone already on the platform.
/// No single exact match is trusted: evaders change one field, and shared Wi-Fi / households are innocent,
/// so weak signals only add up to a manual review, never to an automatic ban.
public static class AccountLinking
{
    public static class Types
    {
        public const string Cccd = "Cccd", Email = "Email", Phone = "Phone", Bank = "Bank", Address = "Address", Ip = "Ip", Geo = "Geo", Device = "Device";
    }

    // (linked to an active account, linked to a banned/rejected account)
    private static readonly Dictionary<string, (int Active, int Banned)> Weights = new()
    {
        [Types.Cccd] = (80, 100),   // same citizen: the project allows one account per CCCD
        [Types.Bank] = (50, 100),   // money goes to the same person
        [Types.Email] = (40, 60),   // same mailbox behind gmail dots / +tags
        [Types.Phone] = (30, 60),
        [Types.Address] = (15, 40),
        [Types.Ip] = (15, 30),      // NAT, cafés, mobile carriers: weak on its own
        [Types.Geo] = (15, 20),
        [Types.Device] = (30, 60),  // same browser (device_id cookie); a shared family PC is innocent, so not decisive
    };
    public const int DisposableEmailWeight = 20, DatacenterIpWeight = 20, NameMismatchWeight = 40;
    public static readonly TimeSpan DeviceMemory = TimeSpan.FromDays(90);

    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "yopmail.com", "10minutemail.com", "guerrillamail.com", "sharklasers.com", "temp-mail.org",
        "tempmail.com", "trashmail.com", "maildrop.cc", "getnada.com", "dispostable.com", "throwawaymail.com",
    };

    /// Own risk signals: throwaway mailbox, signing up from a server/VPN, bank account in someone else's name.
    public static List<RiskSignal> OwnSignals(User user, IEncryptionService encryption)
    {
        var signals = new List<RiskSignal>();
        if (IsDisposableEmail(user.Email)) signals.Add(new("Email dùng một lần", DisposableEmailWeight));
        if (IpIntelligence.DatacenterProvider(user.LastLoginIp) is { } provider)
            signals.Add(new($"IP thuộc datacenter/VPS ({provider}), không phải mạng gia đình/di động", DatacenterIpWeight));
        if (KycName(user.VerificationDocuments, encryption) is { } kyc && BankHolderName(user.BankAccountMock, encryption) is { } holder
            && !NamesMatch(kyc, holder))
            signals.Add(new("Tên chủ tài khoản ngân hàng khác tên trên giấy tờ đã xác minh", NameMismatchWeight));
        return signals;
    }

    public static RiskAssessment Assess(IEnumerable<LinkedAccount> links, IEnumerable<RiskSignal> own)
    {
        var score = 0;
        var reasons = new List<string>();
        // Strongest link per type only: 50 accounts behind one café IP still count once.
        foreach (var byType in links.GroupBy(l => l.Type))
        {
            var banned = byType.Where(l => l.Banned).Select(l => l.UserId).Distinct().ToList();
            var weight = banned.Count > 0 ? Weights[byType.Key].Banned : Weights[byType.Key].Active;
            score += weight;
            var others = byType.Select(l => l.UserId).Distinct().Count();
            reasons.Add(banned.Count > 0
                ? $"{byType.Key}: trùng với {banned.Count} tài khoản đã bị khóa/từ chối (+{weight})"
                : $"{byType.Key}: trùng với {others} tài khoản khác (+{weight})");
        }
        foreach (var signal in own)
        {
            score += signal.Weight;
            reasons.Add($"{signal.Reason} (+{signal.Weight})");
        }
        return new RiskAssessment(score, reasons);
    }

    public static bool IsDisposableEmail(string? email) =>
        email?.Split('@') is [_, var domain] && DisposableDomains.Contains(domain.Trim());

    // Proven bad only. Suspended is temporary and still under investigation: linking to it counts like an active account.
    public static readonly Expression<Func<User, bool>> BannedFilter =
        u => u.Status == "Banned" || u.ApprovalStatus == "Rejected";
    private static readonly Func<User, bool> BannedCheck = BannedFilter.Compile();
    public static bool IsBanned(User u) => BannedCheck(u);

    // ---- normalization: different spellings of the same value must hash the same ----

    public static string? NormalizeEmail(string? email)
    {
        if (email?.Trim().ToLowerInvariant().Split('@') is not [var local, var domain] || local.Length == 0) return null;
        local = local.Split('+')[0];                       // name+shop1@x == name@x
        if (domain is "gmail.com" or "googlemail.com")
        {
            local = local.Replace(".", "");                // n.a.m.e@gmail == name@gmail
            domain = "gmail.com";
        }
        return local + "@" + domain;
    }

    public static string? NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith('0')) digits = "84" + digits[1..]; // 0912... == +84 912...
        return digits.Length >= 9 ? digits : null;
    }

    public static string? NormalizeBankAccount(string? accountNumber)
    {
        var digits = new string((accountNumber ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return digits.Length >= 6 ? digits : null;
    }

    // Words that only say what kind of place follows ("P. Bến Nghé" == "Phường Ben Nghe").
    private static readonly HashSet<string> AddressFiller =
        ["so", "duong", "d", "phuong", "p", "quan", "q", "huyen", "h", "xa", "x", "tp", "thanh", "pho", "tinh", "street", "st", "ward", "district"];

    public static string? NormalizeAddress(string? street, string? city)
    {
        var text = RemoveDiacritics($"{street} {city}".ToLowerInvariant());
        var tokens = new string(text.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !AddressFiller.Contains(t))
            .ToList();
        // A street name alone (no house number) is shared by thousands of people.
        return tokens.Count >= 3 && tokens.Any(t => t.Any(char.IsDigit)) ? string.Join(' ', tokens) : null;
    }

    /// "NGUYEN VAN A" == "Nguyễn  Văn A": bank holder names are usually uppercase without diacritics.
    public static bool NamesMatch(string a, string b)
    {
        static string N(string s) => string.Join(' ', new string(RemoveDiacritics(s.ToLowerInvariant()).Select(c => char.IsLetter(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return N(a) == N(b);
    }

    public static string? KycName(string? verificationDocuments, IEncryptionService encryption) =>
        ReadJsonString(verificationDocuments, encryption, "fullName", "name", "holderName");

    public static string? BankHolderName(string? bankAccountMock, IEncryptionService encryption) =>
        ReadJsonString(bankAccountMock, encryption, "accountName");

    /// First non-empty string property of an (encrypted or legacy plain) JSON object; null when unreadable.
    private static string? ReadJsonString(string? stored, IEncryptionService encryption, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        try
        {
            var json = stored.StartsWith("gcm:", StringComparison.Ordinal) ? encryption.Decrypt(stored) : stored;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var key in keys)
                if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                    return v.GetString();
            return null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            return null; // unreadable data: no signal rather than a crashed review job
        }
    }

    private static string RemoveDiacritics(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    private static readonly IPNetwork[] NotClientNetworks =
        [.. new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "169.254.0.0/16", "::1/128", "fc00::/7", "fe80::/10" }
            .Select(IPNetwork.Parse)];

    /// Private / loopback addresses are our own proxies or a dev machine, not the customer's network.
    public static string? NormalizeIp(string? ip)
    {
        if (!IPAddress.TryParse(ip?.Trim(), out var address)) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return NotClientNetworks.Any(n => n.Contains(address)) ? null : address.ToString();
    }

    /// ~110 m cell, so the same building matches but a GPS jitter of a few metres does not split it.
    public static string? NormalizeGeo(double? lat, double? lng) =>
        lat is { } a && lng is { } b ? FormattableString.Invariant($"{Math.Round(a, 3):F3},{Math.Round(b, 3):F3}") : null;

    /// Identifier hashes of one user. The hash input is prefixed with the type, so equal hashes imply the same type.
    /// deviceHashes: LoginHistory.DeviceHash values of the user (already keyed hashes).
    public static IEnumerable<(string Type, string Hash)> Extract(User user, IEncryptionService encryption, IEnumerable<string>? deviceHashes = null)
    {
        string H(string type, string value) => encryption.ComputeHmac(type.ToLowerInvariant() + ":" + value);

        if (!string.IsNullOrEmpty(user.CccdLookupHash)) yield return (Types.Cccd, user.CccdLookupHash); // already HMAC("cccd:" + value)
        if (NormalizeEmail(user.Email) is { } email) yield return (Types.Email, H(Types.Email, email));
        if (BankAccountNumber(user.BankAccountMock, encryption) is { } bank) yield return (Types.Bank, H(Types.Bank, bank));
        if (NormalizeIp(user.LastLoginIp) is { } ip) yield return (Types.Ip, H(Types.Ip, ip));
        if (NormalizeGeo(user.Latitude, user.Longitude) is { } geo) yield return (Types.Geo, H(Types.Geo, geo));
        foreach (var address in user.Addresses)
        {
            if (NormalizePhone(address.Phone) is { } phone) yield return (Types.Phone, H(Types.Phone, phone));
            if (NormalizeAddress(address.Street, address.City) is { } place) yield return (Types.Address, H(Types.Address, place));
        }
        foreach (var device in deviceHashes ?? []) yield return (Types.Device, device);
    }

    public static async Task<ILookup<int, string>> DeviceHashesAsync(IApplicationDbContext db, IReadOnlyCollection<int> userIds, CancellationToken ct)
    {
        var since = DateTime.UtcNow - DeviceMemory;
        var rows = await db.LoginHistories
            .Where(h => userIds.Contains(h.UserId) && h.DeviceHash != null && h.CreatedAt >= since)
            .Select(h => new { h.UserId, h.DeviceHash }).Distinct().ToListAsync(ct);
        return rows.ToLookup(r => r.UserId, r => r.DeviceHash!);
    }

    private static string? BankAccountNumber(string? bankAccountMock, IEncryptionService encryption) =>
        NormalizeBankAccount(ReadJsonString(bankAccountMock, encryption, "accountNumber"));

    /// Rewrites the identifier rows of the given users from their current data (only the differences are written).
    public static async Task SyncIdentifiersAsync(IApplicationDbContext db, IEncryptionService encryption, IQueryable<User> users, CancellationToken ct)
    {
        // ponytail: loads the selected users with addresses in one go; page through them if the user table gets large
        var loaded = await users.Include(u => u.Addresses).ToListAsync(ct);
        var ids = loaded.Select(u => u.Id).ToList();
        var existing = (await db.UserIdentifiers.Where(i => ids.Contains(i.UserId)).ToListAsync(ct)).ToLookup(i => i.UserId);
        var devices = await DeviceHashesAsync(db, ids, ct);
        foreach (var user in loaded)
        {
            var wanted = Extract(user, encryption, devices[user.Id]).ToHashSet();
            var have = existing[user.Id].ToList();
            db.UserIdentifiers.RemoveRange(have.Where(i => !wanted.Contains((i.Type, i.ValueHash))));
            var haveSet = have.Select(i => (i.Type, i.ValueHash)).ToHashSet();
            db.UserIdentifiers.AddRange(wanted.Where(w => !haveSet.Contains(w))
                .Select(w => new UserIdentifier { UserId = user.Id, Type = w.Type, ValueHash = w.Hash }));
        }
        await db.SaveChangesAsync(ct);
    }

    /// Other accounts sharing at least one identifier with the user (statuses read from tracked entities, so
    /// a decision made earlier in the same unit of work is already visible).
    public static async Task<List<LinkedAccount>> FindLinksAsync(IApplicationDbContext db, User user, IEncryptionService encryption, CancellationToken ct)
    {
        var userId = user.Id;
        var devices = await DeviceHashesAsync(db, [userId], ct);
        var mine = Extract(user, encryption, devices[userId]).Select(x => x.Hash).Distinct().ToList(); // user.Addresses must be loaded
        var matches = await db.UserIdentifiers
            .Where(i => i.UserId != userId && mine.Contains(i.ValueHash))
            .Select(i => new { i.UserId, i.Type })
            .ToListAsync(ct);
        var otherIds = matches.Select(m => m.UserId).Distinct().ToList();
        var others = (await db.Users.Where(u => otherIds.Contains(u.Id)).ToListAsync(ct)).ToDictionary(u => u.Id);
        return matches.Where(m => others.ContainsKey(m.UserId))
            .Select(m => new LinkedAccount(m.UserId, m.Type, IsBanned(others[m.UserId])))
            .ToList();
    }
}
