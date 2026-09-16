using System;
using System.Collections.Generic;

namespace EbayClone.Domain.Entities;

public partial class AdminRole
{
    public int Id { get; set; }

    public string RoleName { get; set; } = string.Empty; // 'SuperAdmin', 'Monitor', 'Support'

    public string? Permissions { get; set; } // JSON: ['ViewDashboard', 'ManageUsers']

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<AdminUserRole> AdminUserRoles { get; set; } = new List<AdminUserRole>();
}
