namespace EbayClone.Domain.Entities;

/// One normalized, keyed-hashed piece of identity (CCCD, email, phone, bank, address, IP, location) of a user.
/// Two users sharing a ValueHash are linked; the raw value is never stored here.
public class UserIdentifier : BaseEntity
{
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ValueHash { get; set; } = string.Empty;
}
