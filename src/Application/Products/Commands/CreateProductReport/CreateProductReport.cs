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
        var entity = new ProductReport
        {
            ProductId = request.ProductId,
            ReporterUserId = request.ReporterUserId,

            ReporterType = "User",

            Reason = request.Reason,

            Description = request.Description, 

            EvidenceFiles = request.EvidenceFiles,

            Status = "Pending",

            Priority = request.Priority
    };

        _context.ProductReports.Add(entity);

        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id; 
    }
}
