using System.Net;

namespace EbayClone.Application.Users.Risk;

public record IpLocation(double Latitude, double Longitude, string Place);

/// Offline IP lookups: "is this a server rather than a home/mobile connection" and "roughly where is it".
public static class IpIntelligence
{
    // ponytail: hand-picked sample of well-known cloud/VPS ranges. Production should load the lists the providers
    // publish (AWS ip-ranges.json, Google cloud.json, Azure ServiceTags) on a schedule instead.
    private static readonly (IPNetwork Network, string Provider)[] Datacenters = Parse(
        ("3.0.0.0/8", "AWS"), ("52.0.0.0/10", "AWS"),
        ("34.64.0.0/10", "Google Cloud"), ("35.192.0.0/12", "Google Cloud"),
        ("20.0.0.0/8", "Microsoft Azure"),
        ("104.248.0.0/16", "DigitalOcean"), ("134.209.0.0/16", "DigitalOcean"), ("159.65.0.0/16", "DigitalOcean"), ("167.99.0.0/16", "DigitalOcean"),
        ("172.104.0.0/15", "Linode"), ("139.162.0.0/16", "Linode"),
        ("95.216.0.0/15", "Hetzner"), ("65.108.0.0/15", "Hetzner"),
        ("45.76.0.0/16", "Vultr"), ("45.77.0.0/16", "Vultr"));

    // ponytail: sample country-level table so the rules can be demoed and tested offline (one machine, IPs sent via
    // X-Forwarded-For). Production: MaxMind GeoLite2 City database. Country-level on purpose: switching between two
    // ISPs of one country must never look like travel.
    private static readonly (IPNetwork Network, IpLocation Location)[] Locations = Parse(
        ("14.160.0.0/11", new IpLocation(21.03, 105.85, "Việt Nam (VNPT)")),
        ("113.160.0.0/11", new IpLocation(21.03, 105.85, "Việt Nam (VNPT)")),
        ("27.64.0.0/12", new IpLocation(21.03, 105.85, "Việt Nam (Viettel)")),
        ("171.224.0.0/11", new IpLocation(21.03, 105.85, "Việt Nam (Viettel)")),
        ("73.0.0.0/8", new IpLocation(39.83, -98.58, "United States (Comcast)")),
        ("126.0.0.0/8", new IpLocation(35.68, 139.69, "Japan (SoftBank)")));

    public static string? DatacenterProvider(string? ip) => Find(ip, Datacenters)?.Value;

    public static IpLocation? Locate(string? ip) => Find(ip, Locations)?.Value;

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
