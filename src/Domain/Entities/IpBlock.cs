namespace EbayClone.Domain.Entities;

/// A network that may not create new accounts (sign-in of existing accounts is never affected: shared IPs).
/// Added automatically (IpBlocklist.AutoBlockAsync) or by an admin; always temporary, and an admin can lift it.
public class IpBlock : BaseEntity
{
    public string Network { get; set; } = "";   // CIDR: "1.2.3.4/32", "2001:db8:1:2::/64"
    public string Reason { get; set; } = "";
    public bool IsAutomatic { get; set; }
    public int? CreatedBy { get; set; }         // admin id; null when automatic
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LiftedAt { get; set; }
    public int? LiftedBy { get; set; }
}
