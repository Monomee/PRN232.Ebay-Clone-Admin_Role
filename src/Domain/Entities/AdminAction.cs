using System;

namespace EbayClone.Domain.Entities;

public partial class AdminAction
{
    public int Id { get; set; }

    public int AdminId { get; set; }

    public string Action { get; set; } = string.Empty; // 'ViewUserProfile', 'BanUser', 'DeleteProduct'

    public string TargetType { get; set; } = string.Empty; // 'User', 'Product', 'Order', 'Review'

    public int? TargetId { get; set; }

    public string? Details { get; set; } // JSON: Action details

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User? Admin { get; set; }
}
