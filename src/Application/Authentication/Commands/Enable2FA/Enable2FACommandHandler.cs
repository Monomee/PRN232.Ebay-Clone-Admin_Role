using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using OtpNet;
using QRCoder;

namespace EbayClone.Application.Authentication.Commands.Enable2FA;
public class Enable2FACommandHandler : IRequestHandler<Enable2FACommand, string>
{
    private readonly IApplicationDbContext _context;

    public Enable2FACommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(Enable2FACommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            throw new Exception("User not found");

        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretKey);

        user.TwoFactorSecret = base32Secret;
        user.TwoFactorEnabled = true;

        await _context.SaveChangesAsync(cancellationToken);

        var otpUri = $"otpauth://totp/EbayClone:{user.Email}?secret={base32Secret}&issuer=EbayClone";

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(otpUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20);

        return Convert.ToBase64String(qrBytes);
    }
}
