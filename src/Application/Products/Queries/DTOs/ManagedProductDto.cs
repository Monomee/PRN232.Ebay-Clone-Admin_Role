namespace EbayClone.Application.Products.Queries.DTOs;
public class ManagedProductDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? ImageUrl { get; set; } // Lấy ảnh đầu tiên
    public decimal Price { get; set; }
    public string? CategoryName { get; set; }
    public string? ShopName { get; set; } // Tên người bán
    public string? Status { get; set; } // Active, Banned...

    public List<string>? ReportReasons { get; set; }

    // --- Thông tin quản lý ---
    public int ReportCount { get; set; }
    public bool IsVeroViolation { get; set; } // Cờ báo hiệu ĐỎ (VeRO)
    public string? Priority { get; set; } // High/Medium/Low
}
