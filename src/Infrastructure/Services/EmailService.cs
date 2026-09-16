using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _config;

    public EmailService(ILogger<EmailService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config; // Tiêm IConfiguration để đọc appsettings.json
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            // 1. Lấy thông tin cấu hình từ appsettings.json
            var smtpServer = _config["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderPassword = _config["EmailSettings:SenderPassword"];

            // Kiểm tra xem đã cấu hình email chưa
            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
            {
                _logger.LogWarning("Chưa cấu hình EmailSettings trong appsettings.json. Hệ thống sẽ bỏ qua việc gửi mail.");
                return;
            }

            // 2. Thiết lập cấu hình SMTP
            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true // Bắt buộc true với Gmail
            };

            // 3. Tạo nội dung Email
            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "eBay Clone Moderation Team"), // Tên người gửi
                Subject = subject,
                Body = body,
                IsBodyHtml = true // Đặt = true để bạn có thể gửi HTML đẹp thay vì text thô
            };

            mailMessage.To.Add(to);

            // 4. Bắn Email
            _logger.LogInformation($"[EmailService] Đang gửi cảnh báo tới {to}...");
            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"[EmailService] Gửi email thành công tới {to}!");
        }
        catch (Exception ex)
        {
            // Bắt lỗi để nếu gửi mail xịt thì hệ thống (ví dụ API xóa sản phẩm) vẫn chạy bình thường
            _logger.LogError($"[EmailService] Lỗi khi gửi email: {ex.Message}");
        }
    }
}
