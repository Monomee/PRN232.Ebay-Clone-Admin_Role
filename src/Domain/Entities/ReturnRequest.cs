using System;
using System.Collections.Generic;

namespace EbayClone.Domain.Entities;

public partial class ReturnRequest
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public int? UserId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }        // "Pending", "Approved", "Rejected"
    public DateTime? CreatedAt { get; set; }

    // Thêm mới
    public string? EvidenceImages { get; set; } // JSON array of image URLs
    public string? ShopSolution { get; set; }   // Giải pháp của shop
    public string? AdminNote { get; set; }       // Ghi chú của admin khi xử lý
    public DateTime? ResolvedAt { get; set; }    // Thời điểm admin xử lý
    public int? ResolvedByAdminId { get; set; }  // Admin nào xử lý

    // Thêm mới cho eBay Refund & Dispute Management
    public bool IsRefundedByEbayFund { get; set; } = false;
    public string? ResolutionAction { get; set; } // "RequireReturn", "KeepItem", "RefundWithoutReturn"
    public string? ReturnLabelUrl { get; set; } // Nhãn vận chuyển trả hàng

    public virtual OrderTable? Order { get; set; }
    public virtual User? User { get; set; }
}
