namespace EbayClone.Application.Products.Commands.CreateProductReport;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

public record CreateProductReportCommand : IRequest<int>
{
    public int ProductId { get; set; }

    public int? ReporterUserId { get; set; }

    public string ReporterType { get; set; } = "User";

    public string? Reason { get; set; } 

    public string? Description { get; set; } // Mô tả chi tiết

    public string? EvidenceFiles { get; set; } // JSON: URLs ảnh/PDF bằng chứng (Quan trọng cho VeRO)

    public string? EvidenceUrl { get; set; } // Alias cho EvidenceFiles nếu client gửi evidenceUrl

    public string Status { get; set; } = "Pending";

    // Mức độ ưu tiên để Admin lọc: 'Low', 'High', 'Critical' (VeRO luôn là Critical)
    public string Priority { get; set; } = "Low";
}

public class CreateProductReportCommandHandler : IRequestHandler<CreateProductReportCommand , int>
{
    private readonly IApplicationDbContext _context;

    public CreateProductReportCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateProductReportCommand request, CancellationToken cancellationToken)
    {
        var reporterType = string.IsNullOrWhiteSpace(request.ReporterType) ? "User" : request.ReporterType.Trim();
        var priority = reporterType.Equals("VeRO", StringComparison.OrdinalIgnoreCase) ? "Critical" : (string.IsNullOrWhiteSpace(request.Priority)
            ? (reporterType.Equals("VeRO", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Low")
            : request.Priority.Trim());
        var evidence = !string.IsNullOrWhiteSpace(request.EvidenceFiles) ? request.EvidenceFiles : request.EvidenceUrl;

        var entity = new ProductReport
        {
            ProductId = request.ProductId,
            ReporterUserId = request.ReporterUserId,

            ReporterType = reporterType,

            Reason = request.Reason,

            Description = request.Description, 

            EvidenceFiles = evidence,

            Status = "Pending",

            Priority = priority
        };

        _context.ProductReports.Add(entity);

        var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
        if (product != null)
        {
            product.ReportCount += 1;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id; 
    }
}
