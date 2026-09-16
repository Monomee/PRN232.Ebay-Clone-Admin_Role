namespace EbayClone.Application.Admin.Disputes.Queries.Common;

public class DisputeDetailDto
{
    public int Id { get; init; }
    public string CaseId { get; init; } = string.Empty;
    public string? Type { get; init; }
    public string? Subcategory { get; init; }
    public string? Status { get; init; }
    public string Priority { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public DateTime? Deadline { get; init; }
    public string? Description { get; init; }
    public string? DesiredOutcome { get; init; }
    
    public OrderInfoDto? Order { get; init; }
    public PartyDto? Buyer { get; init; }
    public PartyDto? Seller { get; init; }
    
    public List<EvidenceDto> BuyerEvidences { get; init; } = new();
    public List<EvidenceDto> SellerEvidences { get; init; } = new();
    
    public List<OfferDto> OfferHistory { get; init; } = new();
    public int NegotiationRounds { get; init; }
    public decimal? LastOfferAmount { get; init; }
    
    public List<DisputeMessageDto> Messages { get; init; } = new();
    public List<TimelineEventDto> Timeline { get; init; } = new();
    
    public DateTime CreatedAt { get; init; }
    public DateTime? FirstResponseAt { get; init; }
    public DateTime? EscalatedAt { get; init; }
    
    public int? AssignedTo { get; init; }
    public string? AssignedToAdminName { get; init; }
    public DateTime? AssignedAt { get; init; }
    public int ViewCount { get; init; }
    public DateTime? LastViewedAt { get; init; }
    public string? AdminNotes { get; init; }
    
    public string? Winner { get; init; }
    public int? ResolvedBy { get; init; }
    public string? ResolvedByAdminName { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public decimal? RefundAmount { get; init; }
    public string? RefundMethod { get; init; }
    public DateTime? RefundProcessedAt { get; init; }
    
    public string? TrackingNumber { get; init; }
    public string? DeliveryStatus { get; init; }
    public bool RequiresReturn { get; init; }
    public string? ReturnTrackingNumber { get; init; }
    
    public bool IsHighValue { get; init; }
    public bool IsVeRO { get; init; }
    
    public TimeSpan? TimeRemaining => Deadline.HasValue 
        ? Deadline.Value - DateTime.UtcNow 
        : null;
    
    public bool IsOverdue => TimeRemaining.HasValue && TimeRemaining.Value.TotalHours < 0;
    public bool IsUrgent => TimeRemaining.HasValue && TimeRemaining.Value.TotalHours <= 24;
    
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
}

public class OrderInfoDto
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public decimal? TotalPrice { get; init; }
    public decimal? PlatformFee { get; init; }
    public decimal? SellerEarnings { get; init; }
    public DateTime? OrderDate { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Status { get; init; }
    public string? ProductTitle { get; init; }
    public string? ProductImage { get; init; }
    public decimal? ProductPrice { get; init; }
}

public class PartyDto
{
    public int Id { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Role { get; init; }
    public int TotalOrders { get; init; }
    public decimal TotalSpent { get; init; }
    public int DisputeCount { get; init; }
    public int ViolationCount { get; init; }
    public DateTime? MemberSince { get; init; }
    public decimal? AverageRating { get; init; }
    public int? TotalReviews { get; init; }
}

public class EvidenceDto
{
    public string Type { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Filename { get; init; }
    public long? FileSize { get; init; }
}

public class OfferDto
{
    public decimal Amount { get; init; }
    public decimal? Percentage { get; init; }
    public string OfferedBy { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public class DisputeMessageDto
{
    public int Id { get; init; }
    public int SenderId { get; init; }
    public string SenderUsername { get; init; } = string.Empty;
    public string SenderType { get; init; } = string.Empty;
    public string MessageType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public List<EvidenceDto> Attachments { get; init; } = new();
    public decimal? OfferAmount { get; init; }
    public string? OfferReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public bool IsInternal { get; init; }
}

public class TimelineEventDto
{
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string? BadgeClass { get; init; }
}
