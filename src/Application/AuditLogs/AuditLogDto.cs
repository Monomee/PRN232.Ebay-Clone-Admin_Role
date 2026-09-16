namespace EbayClone.Application.AuditLogs;

public class AuditLogDto
{
    public int Id { get; set; }
    public int AdminId { get; set; }
    public string? AdminUsername { get; set; }
    public string? User => AdminUsername;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int? TargetId { get; set; }
    public string? TargetName { get; set; }
    public string? Details { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
