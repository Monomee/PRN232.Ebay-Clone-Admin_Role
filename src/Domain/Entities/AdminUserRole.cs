using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EbayClone.Domain.Entities;

public partial class AdminUserRole
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    public int? AssignedBy { get; set; } // Admin ID who assigned

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }

    [ForeignKey(nameof(RoleId))]
    public virtual AdminRole? Role { get; set; }

    [ForeignKey(nameof(AssignedBy))]
    public virtual User? Assigner { get; set; }
}
