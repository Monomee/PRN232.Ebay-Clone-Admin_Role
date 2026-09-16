using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EbayClone.Domain.Entities;

public class ReviewReport
{
    public int Id { get; set; }

    public int ReviewId { get; set; }

    public int? ReporterUserId { get; set; }

    // Reasons like: 'Spam', 'Fake', 'Inappropriate', 'Harassment', 'Other'
    public string Reason { get; set; } = "Other";

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Status: 'Pending', 'Resolved'
    public string Status { get; set; } = "Pending";

    // --- NAVIGATION PROPERTIES ---
    [ForeignKey(nameof(ReviewId))]
    public virtual Review? Review { get; set; }

    [ForeignKey(nameof(ReporterUserId))]
    public virtual User? Reporter { get; set; }
}
