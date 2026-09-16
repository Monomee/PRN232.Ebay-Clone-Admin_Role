using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EbayClone.Domain.Entities;

public partial class UserNotificationRead
{
    public int Id { get; set; }

    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(NotificationId))]
    public virtual Notification Notification { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
