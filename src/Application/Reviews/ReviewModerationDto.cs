using System;
using System.Collections.Generic;

namespace EbayClone.Application.Reviews;

public record ReviewModerationDto
{
    public int Id { get; init; }
    public int? ProductId { get; init; }
    public string? ProductTitle { get; init; }
    public int? ReviewerId { get; init; }
    public string? ReviewerName { get; init; }
    public int? Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string Status { get; init; } = null!;
    public bool FlaggedBySystem { get; init; }
    public string? FlagReason { get; init; }

    // Seller Reports (Legacy/Direct)
    public bool ReportedBySeller { get; init; }
    public string? SellerReportReason { get; init; }
 
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
}
