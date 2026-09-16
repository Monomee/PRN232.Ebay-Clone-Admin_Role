using EbayClone.Domain.Common;

namespace EbayClone.Domain.Entities;

public partial class User : BaseAuditableEntity
{
    public new int Id { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? Role { get; set; }

    public string? AvatarUrl { get; set; }

    // New fields for User Management
    public string Status { get; set; } = "Active"; // 'Active', 'Pending', 'Banned', 'Suspended'
    
    public string ApprovalStatus { get; set; } = "Approved"; // 'Approved', 'PendingApproval', 'Rejected'
    
    public int? ApprovedBy { get; set; } // Admin ID who approved
    
    public DateTime? ApprovedAt { get; set; }
    
    public string? BannedReason { get; set; }
    
    public int? BannedBy { get; set; } // Admin ID who banned
    
    public DateTime? BannedAt { get; set; }
    
    // 2FA fields
    public bool TwoFactorEnabled { get; set; } = false;
    
    public string? TwoFactorSecret { get; set; }
    
    // Security fields
    public string? IpWhitelist { get; set; } // JSON array of allowed IPs
    
    public string? LastLoginIp { get; set; }
    
    public DateTime? LastLoginAt { get; set; }
    
    // Verification fields
    public int ViolationCount { get; set; } = 0; // Tích lũy vi phạm liên quan đến đăng bán sản phẩm
    public DateTime? ProductBanUntil { get; set; } // Khóa quyền đăng sản phẩm đến ngày nào đó
    
    // Review Penalty fields
    public int ReviewViolationCount { get; set; } = 0; // Tích lũy vi phạm đánh giá
    public DateTime? ReviewBanUntil { get; set; } // Khóa quyền review đến ngày nào đó
    public bool IsReviewRestricted { get; set; } = false; // Bị hạn chế chức năng review vĩnh viễn (hoặc tài khoản)
    
    // Seller Performance Management
    public int PerformanceScore { get; set; } = 100; // Điểm uy tín
    
    public bool IsVerified { get; set; } = false; // KYC verification
    
    public string? VerificationDocuments { get; set; } // JSON: CCCD, Business License

    public string SellerLevel { get; set; } = "BelowStandard"; // 'TopRated', 'AboveStandard', 'BelowStandard'

    // Auto-approval Tracking Fields
    public string? CCCD { get; set; } // Identifies a unique citizen
    public double? Latitude { get; set; } // Registration/Last Login Latitude
    public double? Longitude { get; set; } // Registration/Last Login Longitude

    // Payout Engine: Mock bank account info (JSON: { bankName, accountNumber, accountName })
    public string? BankAccountMock { get; set; }
    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    public virtual ICollection<Bid> Bids { get; set; } = new List<Bid>();

    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Message> MessageReceivers { get; set; } = new List<Message>();

    public virtual ICollection<Message> MessageSenders { get; set; } = new List<Message>();

    public virtual ICollection<OrderTable> OrderTables { get; set; } = new List<OrderTable>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Store> Stores { get; set; } = new List<Store>();
}
