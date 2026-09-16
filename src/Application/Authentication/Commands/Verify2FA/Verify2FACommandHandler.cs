using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using EbayClone.Application.Common.Interfaces;

namespace EbayClone.Application.Authentication.Commands.Verify2FA
{
    public class Verify2FACommandHandler
        : IRequestHandler<Verify2FACommand, Verify2FAResponse>
    {
        private readonly IApplicationDbContext _context;
        private readonly IJwtService _jwtService;

        // ✅ Constructor injection (PHẦN BẠN ĐANG THIẾU)
        public Verify2FACommandHandler(
            IApplicationDbContext context,
            IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        public async Task<Verify2FAResponse> Handle(
            Verify2FACommand request,
            CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);

            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
                return new Verify2FAResponse { Success = false, ErrorMessage = "Người dùng không hợp lệ" };

            // ❌ Check Banned Status (thêm check ở đây để bảo mật)
            if (user.Status == "Banned")
                return new Verify2FAResponse { Success = false, ErrorMessage = "Tài khoản đã bị khóa" };

            var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));

            if (!totp.VerifyTotp(request.Code, out long timeStepMatched))
                return new Verify2FAResponse { Success = false, ErrorMessage = "Mã 2FA không đúng hoặc đã hết hạn" };

            // Nếu hợp lệ → tạo JWT
            var token = _jwtService.GenerateToken(user);
            return new Verify2FAResponse { Success = true, Token = token, Role = user.Role };
        }
    }
}
