namespace EbayClone.Application.AdminRoles;

public class AdminRoleDto
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
}

public class AdminUserRoleDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? AssignedBy { get; set; }
    public string? AssignedByUsername { get; set; }
    public DateTime? AssignedAt { get; set; }
}
