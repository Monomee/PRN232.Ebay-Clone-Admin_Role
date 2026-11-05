namespace EbayClone.Application.Users;

public class UserBriefDto
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Active";
    public string ApprovalStatus { get; set; } = "Approved";
    public bool IsVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? SellerLevel { get; set; }
    public int? RiskScore { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; }
    public string? SellerLevel { get; set; }
    
    // Status fields
    public string Status { get; set; } = "Active";
    public string ApprovalStatus { get; set; } = "Approved";
    
    // Ban info
    public string? BannedReason { get; set; }
    public int? BannedBy { get; set; }
    public DateTime? BannedAt { get; set; }
    
    // Approval info
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    // Security & Verification
    public bool TwoFactorEnabled { get; set; }
    public bool IsVerified { get; set; }
    public int ViolationCount { get; set; }
    
    // Login info
    public string? LastLoginIp { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Security-masked sensitive data
    public string? MaskedPhone { get; set; }
    public string? MaskedNationalId { get; set; }

    // Detail screen data
    public List<UserOrderHistoryDto> OrderHistory { get; set; } = new();
    public List<UserViolationHistoryDto> ViolationHistory { get; set; } = new();

    // Account linking: score stored at the sign-up check, links and reasons computed from current data
    public int? RiskScore { get; set; }
    public DateTime? RiskCheckedAt { get; set; }
    public int CurrentRiskScore { get; set; }
    public List<string> RiskReasons { get; set; } = new();
    public List<LinkedAccountDto> LinkedAccounts { get; set; } = new();
    public DateTime? ProductBanUntil { get; set; }
    public DateTime? PayoutHoldUntil { get; set; }
}

public class UserOrderHistoryDto
{
    public int OrderId { get; set; }
    public DateTime? OrderDate { get; set; }
    public string? Status { get; set; }
    public decimal? TotalPrice { get; set; }
}

public class UserViolationHistoryDto
{
    public int AuditLogId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AdminId { get; set; }
    public string? AdminUsername { get; set; }
}

/// Which identity data is shared, never the value itself.
public class LinkedAccountDto
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public string Status { get; set; } = "Active";
    public string ApprovalStatus { get; set; } = "Approved";
    public List<string> SharedTypes { get; set; } = new();
}
