using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users.Risk;

/// Runs on every successful sign-in. It never blocks the sign-in (password + 2FA already passed); it records it and
/// reacts to the signs of a taken-over account: a sign-in from the other side of the world faster than a plane flies.
public static class SignInMonitor
{
    public const double MaxPlausibleSpeedKmh = 900;      // airliner
    public const double MinTravelKm = 500;               // below this, IP geolocation is too coarse to judge
    public static readonly TimeSpan TakeoverPayoutHold = TimeSpan.FromHours(48);

    public static async Task<LoginHistory> RecordAsync(IApplicationDbContext db, int userId, string? ip, string? deviceHash,
        DateTime now, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        var location = IpIntelligence.Locate(ip);
        var flags = new List<string>();

        var datacenter = IpIntelligence.DatacenterProvider(ip);
        if (datacenter != null) flags.Add("Datacenter");

        var previous = await db.LoginHistories.Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt).Take(50).ToListAsync(ct);
        if (deviceHash != null && previous.Count > 0 && previous.All(h => h.DeviceHash != deviceHash)) flags.Add("NewDevice");

        var lastPlace = previous.FirstOrDefault(h => h.Latitude != null && h.Longitude != null);
        if (location != null && lastPlace != null && IsImpossibleTravel(lastPlace, location, now)) flags.Add("ImpossibleTravel");

        var entry = new LoginHistory
        {
            UserId = userId, IpAddress = ip, DeviceHash = deviceHash, Place = location?.Place ?? datacenter,
            Latitude = location?.Latitude, Longitude = location?.Longitude, IsDatacenter = datacenter != null,
            Flags = flags.Count > 0 ? string.Join(',', flags) : null, CreatedAt = now,
        };
        db.LoginHistories.Add(entry);

        user.LastLoginIp = ip;
        user.LastLoginAt = now;
        if (location != null) (user.Latitude, user.Longitude) = (location.Latitude, location.Longitude);

        if (flags.Contains("ImpossibleTravel"))
        {
            // A seller account used from two continents within minutes: money must not leave before the owner reacts.
            if (user.Role == "Seller" && !(user.PayoutHoldUntil > now + TakeoverPayoutHold)) user.PayoutHoldUntil = now + TakeoverPayoutHold;
            db.Notifications.Add(new Notification
            {
                UserId = userId, Title = "New sign-in location",
                Content = $"Your account was signed in from {location!.Place}, far from your previous sign-in ({lastPlace!.Place}). "
                        + "If this was not you, change your password and contact support.",
                Type = "InApp", Status = "Sent", SentAt = now, CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return entry;
    }

    public static bool IsImpossibleTravel(LoginHistory last, IpLocation now, DateTime at)
    {
        var km = IpIntelligence.DistanceKm(last.Latitude!.Value, last.Longitude!.Value, now.Latitude, now.Longitude);
        var hours = Math.Max((at - last.CreatedAt).TotalHours, 1.0 / 60); // same-minute sign-ins: 1 minute
        return km >= MinTravelKm && km / hours > MaxPlausibleSpeedKmh;
    }
}
