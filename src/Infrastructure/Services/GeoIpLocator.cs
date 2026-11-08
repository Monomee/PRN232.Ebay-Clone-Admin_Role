using EbayClone.Application.Users.Risk;
using MaxMind.GeoIP2;

namespace EbayClone.Infrastructure.Services;

/// City-level IP geolocation from a .mmdb file: DB-IP "IP to City Lite" (free, CC BY 4.0, no account needed,
/// https://db-ip.com/db/download/ip-to-city-lite) or MaxMind GeoLite2 City. Not configured: IpIntelligence keeps its
/// built-in sample table.
// ponytail: the file is read once at startup; refresh it monthly by replacing the file and restarting the app.
public static class GeoIpLocator
{
    public static void Configure(string? databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) return;
        if (!File.Exists(databasePath))
            throw new InvalidOperationException($"GeoIp:DatabasePath '{databasePath}' does not exist. Download it (docs/Security-Hardening-2026-11.md, 1.15) or remove the setting.");

        var reader = new DatabaseReader(databasePath); // memory-mapped, thread-safe, lives as long as the app
        IpIntelligence.UseLocator(ip =>
        {
            if (!reader.TryCity(ip, out var city) || city?.Location.Latitude is not { } lat || city.Location.Longitude is not { } lng)
                return null;
            var place = string.Join(", ", new[] { city.City.Name, city.Country.Name }.Where(n => !string.IsNullOrEmpty(n)));
            return new IpLocation(lat, lng, place, city.Country.IsoCode);
        });
    }
}
