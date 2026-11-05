using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Authentication.Commands.Login;

public class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<LoginResponse> Handle(
    LoginCommand request,
    CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Email == request.Email,
                cancellationToken);

        // ❌ Bỏ throw, trả về response có lỗi
        if (user == null)
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = "Email hoặc mật khẩu không đúng"
            };

        if (!PasswordMatches(request.Password, user.Password))
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = "Email hoặc mật khẩu không đúng"
            };

        // ❌ Check Banned Status
        if (user.Status == "Banned" || user.ApprovalStatus == "Rejected")
        {
            var banMsg = "Tài khoản của bạn đã bị khóa";
            if (!string.IsNullOrEmpty(user.BannedReason))
                banMsg += $": {user.BannedReason}";
            
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = banMsg
            };
        }

        if (user.Status == "Unverified")
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = "Vui lòng xác minh email trước khi đăng nhập"
            };

        if (user.TwoFactorEnabled)
            return new LoginResponse
            {
                Success = true,
                RequireTwoFactor = true,
                UserId = user.Id
            };

        var token = _jwtService.GenerateToken(user, await _context.GetAdminPermissionsAsync(user.Id, cancellationToken));
        return new LoginResponse
        {
            Success = true,
            Token = token,
            RequireTwoFactor = false,
            UserId = user.Id,
            Role = user.Role
        };
    }

    // Accounts created before passwords were hashed hold plaintext; BCrypt throws on those instead of returning false.
    private static bool PasswordMatches(string password, string? hash)
    {
        try { return !string.IsNullOrEmpty(hash) && BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
}
