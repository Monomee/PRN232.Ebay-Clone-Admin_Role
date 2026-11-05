namespace EbayClone.Application.Products.Commands.CreateProductReport;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Application.Users.Risk;

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
        // The endpoint sets ReporterUserId from the session; a report always has an accountable account behind it.
        if (request.ReporterUserId is not int reporterId) throw new UnauthorizedAccessException();
        var reporter = await _context.Users.FindAsync(new object[] { reporterId }, cancellationToken);
        AccountStanding.Ensure(reporter); // unverified / pending / suspended / banned accounts cannot report

        var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} not found.");

        // One open report per person per product: repeating it must not push the listing up the moderation queue.
        var open = await _context.ProductReports.FirstOrDefaultAsync(r => r.ProductId == request.ProductId
            && r.ReporterUserId == reporterId && r.Status == "Pending", cancellationToken);
        if (open != null) return open.Id;

        // The server decides type and priority: a client asking for "Critical" must not jump the queue.
        // "System" is only written by the moderation job, never accepted from a request.
        var reporterType = string.Equals(request.ReporterType?.Trim(), "VeRO", StringComparison.OrdinalIgnoreCase) ? "VeRO" : "User";
        var priority = reporterType == "VeRO" ? "Critical" : "Low";
        var evidence = !string.IsNullOrWhiteSpace(request.EvidenceFiles) ? request.EvidenceFiles : request.EvidenceUrl;

        var entity = new ProductReport
        {
            ProductId = request.ProductId,
            ReporterUserId = reporterId,
            ReporterType = reporterType,
            Reason = request.Reason,
            Description = request.Description,
            EvidenceFiles = evidence,
            Status = "Pending",
            Priority = priority
        };

        _context.ProductReports.Add(entity);
        product.ReportCount += 1;
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
