namespace EbayClone.Application.Broadcasts;

public class BroadcastDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? UserRole { get; set; } // Target audience: All, Buyer, Seller
    public string Type { get; set; } = "InApp"; // Email, InApp
    public string Status { get; set; } = "Pending";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int? CreatedBy { get; set; }
    public string? CreatedByUsername { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
