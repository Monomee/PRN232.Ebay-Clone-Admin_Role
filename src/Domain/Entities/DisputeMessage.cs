namespace EbayClone.Domain.Entities;

public class DisputeMessage
{
    public int Id { get; set; }
    
    public int DisputeId { get; set; }
    
    public int SenderId { get; set; }
    
    public string SenderType { get; set; } = string.Empty; // 'Buyer', 'Seller', 'Admin', 'System'
    
    public string MessageType { get; set; } = "Response"; // 'Response', 'Offer', 'Accept', 'Decline', 'Evidence', 'Note'
    
    public string Content { get; set; } = string.Empty;
    
    public string? Attachments { get; set; } // JSON: [{url, type, filename}]
    
    public decimal? OfferAmount { get; set; } // If message type is 'Offer'
    
    public string? OfferReason { get; set; } // Why offering this amount
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsRead { get; set; } = false;
    
    public DateTime? ReadAt { get; set; }
    
    public bool IsInternal { get; set; } = false; // Admin-only note
    
    // Navigation properties
    public virtual Dispute Dispute { get; set; } = null!;
    
    public virtual User Sender { get; set; } = null!;
}
