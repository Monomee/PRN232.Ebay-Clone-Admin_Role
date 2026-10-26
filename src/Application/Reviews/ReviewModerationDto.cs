using System;
using System.Collections.Generic;

namespace EbayClone.Application.Reviews;

public record ReviewModerationDto
{
    public int Id { get; init; }
    public int? ProductId { get; init; }
    public string? ProductTitle { get; init; }
    public int? SellerId { get; init; }
    public string? SellerName { get; init; }
    public int? ReviewerId { get; init; }
    public string? ReviewerName { get; init; }
    public int? Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string Status { get; init; } = null!;
    public bool FlaggedBySystem { get; init; }
    public string? FlagReason { get; init; }

    // Seller Reports & Replies
    public bool ReportedBySeller { get; init; }
    public string? SellerReportReason { get; init; }
    public string? SellerReply { get; init; }
    public DateTime? SellerReplyCreatedAt { get; init; }

    // Moderation Audit
    public int? ModeratedBy { get; init; }
    public string? ModeratedByName { get; init; }
    public DateTime? ModeratedAt { get; init; }
    public string? ModerationAction { get; init; }
 
    // Generic Reports
    public List<ReviewReportDto> Reports { get; init; } = new();
}

public record ReviewReportDto
{
    public int Id { get; init; }
    public string? ReporterName { get; init; }
    public string Reason { get; init; } = null!;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Status { get; init; }
}

public record ReviewStatisticsDto
{
    public int TotalReviews { get; init; }
    public int FlaggedCount { get; init; }
    public int PendingCount { get; init; }
    public int HiddenCount { get; init; }
    public int VisibleCount { get; init; }
    public int SellerReportedCount { get; init; }
    public int CommunityReportedCount { get; init; }
    public decimal AverageRating { get; init; }
    public Dictionary<int, int> RatingDistribution { get; init; } = new();
}

public record SellerFeedbackDto
{
    public int Id { get; init; }
    public int? SellerId { get; init; }
    public string? SellerName { get; init; }
    public string? SellerEmail { get; init; }
    public decimal? AverageRating { get; init; }
    public int? TotalReviews { get; init; }
    public decimal? PositiveRate { get; init; }
    public string? SellerStatus { get; init; }
    public string? ReputationTier { get; init; } // 'TopRated', 'AboveStandard', 'BelowStandard'
}
