using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Reviews.Commands;

public record UpdateReviewStatusCommand : IRequest<bool>
{
    public int Id { get; init; }
    public string Status { get; init; } = null!; // 'Visible', 'Hidden'
    public string Action { get; init; } = null!; // 'Keep', 'Hide'
    public int AdminId { get; init; }
}

public class UpdateReviewStatusCommandHandler : IRequestHandler<UpdateReviewStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public UpdateReviewStatusCommandHandler(IApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<bool> Handle(UpdateReviewStatusCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .Include(r => r.Reviewer)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (review == null) return false;

        review.Status = request.Status;
        review.ModerationAction = request.Action;
        review.ModeratedBy = request.AdminId;
        review.ModeratedAt = DateTime.UtcNow;
        
        // Staggered Penalty System for Reviews
        if (request.Action == "Hide")
        {
            var reviewer = review.Reviewer;
            if (reviewer != null)
            {
                reviewer.ReviewViolationCount += 1;
                
                string emailSubject = "";
                string emailBody = "";

                if (reviewer.ReviewViolationCount == 1)
                {
                    // 1. Xóa review + cảnh báo
                    emailSubject = "CẢNH BÁO VI PHẠM ĐÁNH GIÁ SẢN PHẨM";
                    emailBody = $"Đánh giá của bạn đã bị xóa do vi phạm tiêu chuẩn cộng đồng. Vui lòng tuân thủ quy định để tránh bị hạn chế chức năng.";
                }
                else if (reviewer.ReviewViolationCount == 2)
                {
                    // 2. Cấm review 7 ngày
                    reviewer.ReviewBanUntil = DateTime.UtcNow.AddDays(7);
                    emailSubject = "TẠM KHÓA CHỨC NĂNG ĐÁNH GIÁ 7 NGÀY";
                    emailBody = $"Bạn đã vi phạm quy định đánh giá lần 2. Chức năng đánh giá của bạn bị tạm khóa trong 7 ngày.";
                }
                else if (reviewer.ReviewViolationCount == 3)
                {
                    // 3. Hạn chế chức năng vĩnh viễn
                    reviewer.IsReviewRestricted = true;
                    emailSubject = "HẠN CHẾ CHỨC NĂNG ĐÁNH GIÁ VĨNH VIỄN";
                    emailBody = $"Bạn đã vi phạm quy định đánh giá lần 3. Chức năng đánh giá của bạn đã bị hạn chế vĩnh viễn. Nếu tiếp tục vi phạm, tài khoản sẽ bị khóa.";
                }
                else if (reviewer.ReviewViolationCount >= 4)
                {
                    // 4. Khóa tài khoản
                    reviewer.Status = "Banned";
                    reviewer.BannedReason = "Tài khoản bị khóa do vi phạm lặp đi lặp lại quy định về đánh giá.";
                    reviewer.BannedAt = DateTime.UtcNow;
                    emailSubject = "TÀI KHOẢN ĐÃ BỊ KHÓA VĨNH VIỄN";
                    emailBody = $"Tài khoản của bạn đã bị khóa do cố tình vi phạm nhiều lần tiêu chuẩn cộng đồng. Vui lòng liên hệ Admin để biết thêm chi tiết.";
                }

                if (!string.IsNullOrEmpty(reviewer.Email))
                {
                    await _emailService.SendEmailAsync(reviewer.Email, emailSubject, emailBody);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
