namespace EbayClone.Application.Admin.Disputes.Queries.Common;

public class DisputeDto
{
    public int Id { get; init; }
    public string CaseId { get; init; } = string.Empty;
    public int? OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string? Type { get; init; }
    public string? Subcategory { get; init; }
    public string? Status { get; init; }
    public string Priority { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public DateTime? Deadline { get; init; }
    public string? Description { get; init; }
    public string? DesiredOutcome { get; init; }
    
    // Parties
    public int? BuyerId { get; init; }
    public string? BuyerUsername { get; init; }
    public string? BuyerEmail { get; init; }
    
    public int? SellerId { get; init; }
    public string? SellerUsername { get; init; }
    public string? SellerEmail { get; init; }
    
    // Product info
    public string? ProductTitle { get; init; }
    public string? ProductImage { get; init; }
    public decimal? ProductPrice { get; init; }
    
    // Timeline
    public DateTime CreatedAt { get; init; }
    public DateTime? EscalatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    
    // Assignment
    public int? AssignedTo { get; init; }
    public string? AssignedToAdminName { get; init; }
    
    // Resolution
    public string? Winner { get; init; }
    public decimal? RefundAmount { get; init; }
    
    // UI helpers
    public string PriorityBadgeClass => Priority?.ToLower() switch
    {
        "critical" => "badge-danger",
        "high" => "badge-warning",
        "medium" => "badge-info",
        _ => "badge-secondary"
    };
    
    public string StatusBadgeClass => Status?.ToLower() switch
    {
        "escalated" => "badge-danger",
        "underreview" => "badge-warning",
        "resolved" => "badge-success",
        "closed" => "badge-secondary",
        _ => "badge-info"
    };
    
    public string TypeBadgeClass => Type?.ToUpper() switch
    {
        "INR" => "badge-danger",
        "INAD" => "badge-warning",
        "COUNTERFEIT" => "badge-dark",
        _ => "badge-info"
    };
    
    public TimeSpan? TimeRemaining => Deadline.HasValue 
        ? Deadline.Value - DateTime.UtcNow 
        : null;
    
    public bool IsUrgent => TimeRemaining.HasValue && TimeRemaining.Value.TotalHours <= 24;
}
