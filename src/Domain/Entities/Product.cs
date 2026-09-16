using System;
using System.Collections.Generic;

namespace EbayClone.Domain.Entities;

public class Product : BaseAuditableEntity
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public string? Images { get; set; }

    public int? CategoryId { get; set; }

    public int? SellerId { get; set; }

    public bool? IsAuction { get; set; }

    public DateTime? AuctionEndTime { get; set; }

    // New fields for Product Management & Moderation
    public string Status { get; set; } = "Active"; // 'Active', 'Hidden', 'Reported', 'Deleted'
    
    public int ReportCount { get; set; } = 0;
    
    public bool IsVerified { get; set; } = false; // Admin approved
    
    public int? VerifiedBy { get; set; } // Admin ID
    
    public DateTime? VerifiedAt { get; set; }
    
    public string? ViolationType { get; set; } // 'Copyright', 'Fake', 'Inappropriate', NULL
    
    public string? ModerationNotes { get; set; } // Admin notes

    public virtual ICollection<Bid> Bids { get; set; } = new List<Bid>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual User? Seller { get; set; }

    public virtual ICollection<ProductReport> Reports { get; set; } = new List<ProductReport>();
}

