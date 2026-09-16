using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Products.Commands.ResolveProductViolation;
public enum ViolationResolutionAction
{
    DeleteAndWarn = 1, // Xóa & Cảnh báo (Cho VeRO)
    Hide = 2,          // Ẩn sản phẩm
    Reject = 3,         // Từ chối báo cáo (Báo cáo sai)
    Restore = 4
}

public record ResolveProductViolationCommand : IRequest
{
    public int ProductId { get; init; }
    public ViolationResolutionAction Action { get; init; }
    public string? AdminNote { get; init; }
    public string? ViolationType { get; init; }
}

public class ResolveProductViolationCommandHandler : IRequestHandler<ResolveProductViolationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly INotificationNotifier _notifier;

    public ResolveProductViolationCommandHandler(IApplicationDbContext context, IEmailService emailService, INotificationNotifier notifier)
    {
        _context = context;
        _emailService = emailService;
        _notifier = notifier;
    }

    public async Task Handle(ResolveProductViolationCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Seller)
            .Include(p => p.Reports.Where(r => r.Status == "Pending"))
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null) throw new NotFoundException(nameof(Product), $"{request.ProductId}");

        // 1. Cập nhật Trạng thái Sản phẩm & Report dựa trên Quyết định
        if (request.Action == ViolationResolutionAction.DeleteAndWarn)
        {
            product.Status = "Banned"; // Soft Delete
            product.ViolationType = request.ViolationType ?? "Other"; // Gắn cờ vi phạm bản quyền
            MarkReportsAs(product.Reports, "Resolved");

            var seller = product.Seller;
            if (seller != null)
            {
                seller.ViolationCount += 1;

                if (seller.ViolationCount >= 3)
                {
                    seller.Status = "Banned";
                    seller.BannedReason = "Tài khoản bị khóa tự động do có quá nhiều vi phạm đăng bán sản phẩm theo quyết định của Admin.";
                    seller.BannedAt = DateTime.UtcNow;

                    if (!string.IsNullOrEmpty(seller.Email))
                    {
                        await _emailService.SendEmailAsync(
                            seller.Email,
                            "TÀI KHOẢN ĐÃ BỊ KHÓA VĨNH VIỄN",
                            $"Tài khoản của bạn đã bị khóa do vi phạm bản quyền/hàng cấm nhiều lần. Sản phẩm '{product.Title}' là vi phạm thứ {seller.ViolationCount}. Vui lòng liên hệ Admin.");
                    }
                }
                else
                {
                    // 2. Gửi Email tự động cho người bán
                    if (!string.IsNullOrEmpty(seller.Email))
                    {
                        await _emailService.SendEmailAsync(
                            seller.Email,
                            "CẢNH BÁO VI PHẠM BẢN QUYỀN (VeRO)",
                            $"Sản phẩm '{product.Title}' của bạn đã bị xóa do vi phạm sở hữu trí tuệ. Bạn đã vi phạm {seller.ViolationCount}/3 lần. Nếu chạm mốc 3 lần, tài khoản sẽ bị khóa.");
                    }
                }
            }
        }
        else if (request.Action == ViolationResolutionAction.Hide)
        {
            product.Status = "Hidden";
            // 1. Ghi nhận lỗi nhẹ khiến sản phẩm bị ẩn
            product.ViolationType = request.ViolationType ?? "Minor Policy Violation";
            MarkReportsAs(product.Reports, "Resolved");

            // 2. [Tùy chọn] Gửi email yêu cầu Seller sửa lại
            if (!string.IsNullOrEmpty(product.Seller?.Email))
            {
                await _emailService.SendEmailAsync(
                    product.Seller.Email,
                    "SẢN PHẨM BỊ TẠM ẨN",
                    $"Sản phẩm '{product.Title}' của bạn đang bị tạm ẩn do: {product.ViolationType}. Vui lòng cập nhật lại thông tin cho đúng quy định.");
            }
        }
        else if (request.Action == ViolationResolutionAction.Reject)
        {
            product.Status = "Active";
            product.ReportCount = 0;
            product.ViolationType = null; 
            MarkReportsAs(product.Reports, "Rejected");
        }
        else if (request.Action == ViolationResolutionAction.Restore)
        {
            product.Status = "Active";
            product.ViolationType = null; 
            product.ReportCount = 0;

            // LƯU Ý KỸ: Chúng ta KHÔNG chạm vào product.Reports ở nhánh này.
            // Vì những ticket report cũ kia đã đóng (Resolved/Approved) rồi, 
            // giữ nguyên để làm bằng chứng là "đã từng có người report và admin đã từng xử lý".

            if (!string.IsNullOrEmpty(product.Seller?.Email))
            {
                await _emailService.SendEmailAsync(
                    product.Seller.Email,
                    "THÔNG BÁO KHÔI PHỤC SẢN PHẨM",
                    $"Chào bạn, sau khi xem xét lại kháng cáo, chúng tôi đã khôi phục sản phẩm '{product.Title}'. Xin lỗi vì sự bất tiện này!");
            }
        }

        // Lưu vết Admin xử lý
        product.IsVerified = true;
        // product.VerifiedBy = ... (Lấy từ CurrentUserService)
        product.VerifiedAt = DateTime.UtcNow;
        product.ModerationNotes = request.AdminNote;
        product.ReportCount = 0;
        await _context.SaveChangesAsync(cancellationToken);

        // Broadcast real-time notification when product is banned/hidden
        if (request.Action == ViolationResolutionAction.DeleteAndWarn || request.Action == ViolationResolutionAction.Hide)
        {
            var reason = request.Action == ViolationResolutionAction.DeleteAndWarn
                ? "This product has been removed due to policy violation."
                : "This product has been temporarily hidden for review.";
            await _notifier.NotifyProductBannedAsync(request.ProductId, reason, cancellationToken);
        }
    }

    private void MarkReportsAs(IEnumerable<ProductReport> reports, string status)
    {
        foreach (var report in reports)
        {
            report.Status = status;
        }
    }
}
