using System;
using System.Collections.Generic;

namespace EbayClone.Domain.Entities;

public partial class Review
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public int? ReviewerId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime? CreatedAt { get; set; }

    // New fields for Review Moderation
    public string Status { get; set; } = "Visible"; // 'Visible', 'Hidden', 'PendingReview'
    
    public bool FlaggedBySystem { get; set; } = false; // Bot detected bad words
    
    public string? FlagReason { get; set; } // 'BadWords', 'Spam', 'LowRating'
    
    public int? ModeratedBy { get; set; } // Admin ID
    
    public DateTime? ModeratedAt { get; set; }
    
    public string? ModerationAction { get; set; } // 'Keep', 'Hide', NULL

    // New fields for Seller Replies and Reports
    public string? SellerReply { get; set; }
    
    public DateTime? SellerReplyCreatedAt { get; set; }
    
    public bool ReportedBySeller { get; set; } = false;
    
    public string? SellerReportReason { get; set; }

    public virtual Product? Product { get; set; }

    public virtual User? Reviewer { get; set; }
}
