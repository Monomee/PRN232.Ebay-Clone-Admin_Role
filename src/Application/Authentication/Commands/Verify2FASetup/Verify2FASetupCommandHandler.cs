using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using OtpNet;

namespace EbayClone.Application.Authentication.Commands.Verify2FASetup;
public class Verify2FASetupCommandHandler
    : IRequestHandler<Verify2FASetupCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public Verify2FASetupCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        Verify2FASetupCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);

        if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
            return false;

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));

        // Verify mã — dùng window = 1 để chấp nhận lệch 30s
        var isValid = totp.VerifyTotp(
            request.Code,
            out long _,
            window: VerificationWindow.RfcSpecifiedNetworkDelay
        );

        return isValid;
    }
}
