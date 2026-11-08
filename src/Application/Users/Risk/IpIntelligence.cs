using System.Net;
using System.Text.Json;

namespace EbayClone.Application.Users.Risk;

/// Country: ISO code; sign-ins within one country never count as travel (see SignInMonitor.IsImpossibleTravel).
public record IpLocation(double Latitude, double Longitude, string Place, string? Country = null);

/// Offline IP lookups: "is this a server rather than a home/mobile connection" and "roughly where is it".
public static class IpIntelligence
{
    // Hand-picked sample of well-known cloud/VPS ranges. DatacenterRangesService replaces the AWS and Google Cloud rows
    // with the full lists those providers publish (daily); the others have no stable public feed and keep the sample.
    // ponytail: Azure ServiceTags needs a download URL that changes weekly; 20.0.0.0/8 stays as its sample.
    private static readonly (IPNetwork Network, string Provider)[] SampleDatacenters = Parse(
        ("3.0.0.0/8", "AWS"), ("52.0.0.0/10", "AWS"),
        ("34.64.0.0/10", "Google Cloud"), ("35.192.0.0/12", "Google Cloud"),
        ("20.0.0.0/8", "Microsoft Azure"),
        ("104.248.0.0/16", "DigitalOcean"), ("134.209.0.0/16", "DigitalOcean"), ("159.65.0.0/16", "DigitalOcean"), ("167.99.0.0/16", "DigitalOcean"),
        ("172.104.0.0/15", "Linode"), ("139.162.0.0/16", "Linode"),
        ("95.216.0.0/15", "Hetzner"), ("65.108.0.0/15", "Hetzner"),
        ("45.76.0.0/16", "Vultr"), ("45.77.0.0/16", "Vultr"));

    // Fallback when no city database is configured (GeoIp:DatabasePath, see UseLocator) and for offline tests
    // (one machine, IPs sent via X-Forwarded-For).
    private static readonly (IPNetwork Network, IpLocation Location)[] Locations = Parse(
        ("14.160.0.0/11", new IpLocation(21.03, 105.85, "Việt Nam (VNPT)", "VN")),
        ("113.160.0.0/11", new IpLocation(21.03, 105.85, "Việt Nam (VNPT)", "VN")),
        ("27.64.0.0/12", new IpLocation(21.03, 105.85, "Việt Nam (Viettel)", "VN")),
        ("171.224.0.0/11", new IpLocation(21.03, 105.85, "Việt Nam (Viettel)", "VN")),
        ("73.0.0.0/8", new IpLocation(39.83, -98.58, "United States (Comcast)", "US")),
        ("126.0.0.0/8", new IpLocation(35.68, 139.69, "Japan (SoftBank)", "JP")));

    private static volatile (IPNetwork Network, string Provider)[] _datacenters = SampleDatacenters;

    public static string? DatacenterProvider(string? ip) => Find(ip, _datacenters)?.Value;

    /// Swaps in downloaded ranges: every provider present in `ranges` loses its current rows (sample or an earlier
    /// download), the others keep theirs, so one failed feed never drops another provider's list.
    // ponytail: linear scan over ~20k ranges (~0.1 ms) per check; a sorted range table if this shows up in profiles
    public static void UseDatacenterRanges(IReadOnlyCollection<(IPNetwork Network, string Provider)> ranges)
    {
        var providers = ranges.Select(r => r.Provider).ToHashSet();
        _datacenters = [.. ranges, .. _datacenters.Where(s => !providers.Contains(s.Provider))];
    }

    /// AWS ip-ranges.json: EC2 only (rented servers), not Amazon's own retail/corporate ranges.
    public static List<(IPNetwork, string)> ParseAwsRanges(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return [.. root.GetProperty("prefixes").EnumerateArray().Concat(root.GetProperty("ipv6_prefixes").EnumerateArray())
            .Where(p => p.GetProperty("service").GetString() == "EC2")
            .Select(p => p.TryGetProperty("ip_prefix", out var v4) ? v4.GetString() : p.GetProperty("ipv6_prefix").GetString())
            .Distinct()
            .Select(cidr => (IPNetwork.Parse(cidr!), "AWS"))];
    }

    /// Google cloud.json: Google Cloud customer ranges (not Google's own services like Search or Gmail).
    public static List<(IPNetwork, string)> ParseGoogleCloudRanges(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return [.. doc.RootElement.GetProperty("prefixes").EnumerateArray()
            .Select(p => p.TryGetProperty("ipv4Prefix", out var v4) ? v4.GetString() : p.GetProperty("ipv6Prefix").GetString())
            .Distinct()
            .Select(cidr => (IPNetwork.Parse(cidr!), "Google Cloud"))];
    }

    private static volatile Func<IPAddress, IpLocation?>? _locator;

    /// City-level database lookup (DB-IP Lite / MaxMind GeoLite2 .mmdb), set once at startup by Infrastructure.
    public static void UseLocator(Func<IPAddress, IpLocation?>? locator) => _locator = locator;

    public static IpLocation? Locate(string? ip)
    {
        if (_locator is { } locator && IPAddress.TryParse(ip?.Trim(), out var address)
            && locator(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address) is { } found)
            return found;
        return Find(ip, Locations)?.Value;
    }

    /// Great-circle distance (haversine).
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lng2 - lng1) / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(a));
    }

    private static (IPNetwork, T)[] Parse<T>(params (string Cidr, T Value)[] items) =>
        [.. items.Select(i => (IPNetwork.Parse(i.Cidr), i.Value))];

    private static (IPNetwork Network, T Value)? Find<T>(string? ip, (IPNetwork Network, T Value)[] table)
    {
        if (!IPAddress.TryParse(ip?.Trim(), out var address)) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        foreach (var row in table)
            if (row.Network.Contains(address)) return row;
        return null;
    }
}
