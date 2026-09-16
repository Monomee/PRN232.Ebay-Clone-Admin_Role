using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EbayClone.Application.Products.Commands.CreateProduct;
public record CreateProductCommand : IRequest<int>
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Images { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsAuction { get; set; }
    // SellerId thường lấy từ CurrentUserService (người đang đăng nhập), 
    // nhưng ở đây tôi để tạm là tham số truyền vào để bạn dễ test.
    public int? SellerId { get; set; }
}

// 2. Định nghĩa Handler (Logic xử lý)
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;

    public CreateProductCommandHandler(IApplicationDbContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.SellerId.HasValue)
        {
            var seller = await _context.Users.FindAsync(new object[] { request.SellerId.Value }, cancellationToken);
            if (seller != null)
            {
                if (seller.Status == "Banned")
                    throw new Exception("Tài khoản của bạn đã bị khóa.");
                if (seller.ProductBanUntil.HasValue && seller.ProductBanUntil.Value > DateTime.UtcNow)
                    throw new Exception($"Chức năng đăng sản phẩm của bạn bị tạm khóa đến {seller.ProductBanUntil.Value:dd/MM/yyyy HH:mm}.");
            }
        }

        var entity = new Product
        {
            Title = request.Title,
            Description = request.Description,
            Price = request.Price,
            Images = request.Images,
            CategoryId = request.CategoryId,
            SellerId = request.SellerId,
            IsAuction = request.IsAuction,
            Status = "Active", // Mặc định cho phép hiển thị
            ReportCount = 0
        };

        _context.Products.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        // Chạy ngầm AI Moderation cho Tiêu đề sản phẩm
        if (!string.IsNullOrEmpty(entity.Title))
        {
            var productId = entity.Id;
            var titleToCheck = entity.Title;

            _ = Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var moderationService = scope.ServiceProvider.GetRequiredService<IContentModerationService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                try
                {
                    var (isFlagged, reason) = await moderationService.ModerateContentAsync(titleToCheck, default);
                    
                    if (isFlagged)
                    {
                        var p = await dbContext.Products
                            .Include(x => x.Seller)
                            .FirstOrDefaultAsync(x => x.Id == productId);

                        if (p != null)
                        {
                            p.Status = "Hidden";
                            p.ViolationType = reason;
                            
                            var seller = p.Seller;
                            if (seller != null)
                            {
                                seller.ViolationCount += 1;
                                
                                string emailSubject = "";
                                string emailBody = "";

                                if (seller.ViolationCount == 1)
                                {
                                    // Lần 1: Xóa sản phẩm + warning
                                    emailSubject = "CẢNH BÁO VI PHẠM ĐĂNG BÁN SẢN PHẨM";
                                    emailBody = $"Sản phẩm '{p.Title}' của bạn đã bị xóa do vi phạm: {reason}. Đây là vi phạm lần thứ 1. Vui lòng tuân thủ quy định.";
                                }
                                else if (seller.ViolationCount == 2)
                                {
                                    // Lần 2: Khóa đăng sản phẩm 7 ngày
                                    seller.ProductBanUntil = DateTime.UtcNow.AddDays(7);
                                    emailSubject = "TẠM KHÓA CHỨC NĂNG ĐĂNG BÁN 7 NGÀY";
                                    emailBody = $"Sản phẩm '{p.Title}' của bạn đã bị xóa do vi phạm: {reason}. Bạn đã vi phạm lần 2. Chức năng đăng sản phẩm bị tạm khóa trong 7 ngày.";
                                }
                                else if (seller.ViolationCount >= 3)
                                {
                                    // Lần 3: Khóa đăng sản phẩm 30 ngày
                                    seller.ProductBanUntil = DateTime.UtcNow.AddDays(30);
                                    emailSubject = "TẠM KHÓA CHỨC NĂNG ĐĂNG BÁN 30 NGÀY";
                                    emailBody = $"Sản phẩm '{p.Title}' của bạn đã bị xóa do vi phạm: {reason}. Bạn đã vi phạm lần {seller.ViolationCount}. Chức năng đăng sản phẩm bị tạm khóa liên tục trong 30 ngày. Tái phạm tài khoản sẽ bị khóa.";
                                }

                                if (!string.IsNullOrEmpty(seller.Email))
                                {
                                    await emailService.SendEmailAsync(seller.Email, emailSubject, emailBody);
                                }
                            }

                            await dbContext.SaveChangesAsync(default);
                        }
                    }
                }
                catch
                {
                    // Chạy ngầm lỗi thì bỏ qua, không làm sập tiến trình
                }
            });
        }

        return entity.Id; // Trả về ID của sản phẩm vừa tạo
    }
}
