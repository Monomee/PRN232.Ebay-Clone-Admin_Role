namespace EbayClone.Domain.Entities;

/// One successful sign-in (token issued). Feeds device linking, impossible-travel and datacenter checks,
/// and is the audit trail of where an account was used from.
public class LoginHistory : BaseEntity
{
    public int UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceHash { get; set; }   // HMAC of the device_id cookie, never the cookie itself
    public string? Place { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsDatacenter { get; set; }
    public string? Flags { get; set; }        // comma separated: ImpossibleTravel, Datacenter, NewDevice
    public DateTime CreatedAt { get; set; }
}
