using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EbayClone.Domain.Entities;

public partial class Notification
{
    public int Id { get; set; }

    public int? UserId { get; set; } // NULL = broadcast to all

    public string? UserRole { get; set; } // 'All', 'Buyer', 'Seller', 'Admin', NULL

    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string Type { get; set; } = "InApp"; // 'Email', 'InApp', 'SMS'

    public string Status { get; set; } = "Pending"; // 'Pending', 'Sent', 'Scheduled'

    public DateTime? ScheduledAt { get; set; }

    public DateTime? SentAt { get; set; }

    public int? CreatedBy { get; set; } // Admin ID

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public virtual User? Creator { get; set; }
}
