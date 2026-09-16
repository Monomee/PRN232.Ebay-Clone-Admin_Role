using System.ComponentModel.DataAnnotations.Schema;
using EbayClone.Domain.Common;

namespace EbayClone.Domain.Entities;

public class ProductReport : BaseAuditableEntity
{
    public int ProductId { get; set; }

    // Người báo cáo (Nếu là Guest/Anonymous thì null, nếu là Brand Owner thì bắt buộc có acc)
    public int? ReporterUserId { get; set; }

    // Phân loại: 'User' (người mua báo cáo hàng đểu), 'VeRO' (chủ thương hiệu báo cáo bản quyền), 'System' (AI quét)
    public string ReporterType { get; set; } = "User";

    public string? Reason { get; set; } // VD: "Hàng giả", "Ảnh mờ"

    public string? Description { get; set; } // Mô tả chi tiết

    public string? EvidenceFiles { get; set; } // JSON: URLs ảnh/PDF bằng chứng (Quan trọng cho VeRO)

    // Status: 'Pending' (Chờ xử lý), 'Approved' (Đúng là vi phạm -> Đã xóa SP), 'Rejected' (Báo cáo sai -> Bỏ qua)
    public string Status { get; set; } = "Pending";

    // Mức độ ưu tiên để Admin lọc: 'Low', 'High', 'Critical' (VeRO luôn là Critical)
    public string Priority { get; set; } = "Low";

    // --- ADMIN XỬ LÝ ---
    public int? ResolvedBy { get; set; } // Admin ID nào xử lý
    public DateTime? ResolvedAt { get; set; }
    public string? AdminReply { get; set; } // Admin trả lời lại người báo cáo (nếu cần)

    // --- NAVIGATION PROPERTIES ---
    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }

    [ForeignKey(nameof(ReporterUserId))]
    public virtual User? ReporterUser { get; set; }

    [ForeignKey(nameof(ResolvedBy))]
    public virtual User? Resolver { get; set; }
}
