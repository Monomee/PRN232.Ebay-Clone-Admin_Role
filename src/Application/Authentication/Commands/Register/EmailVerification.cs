using System.Security.Cryptography;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EbayClone.Application.Authentication.Commands.Register;

/// One-time code proving the user owns the mailbox. Only an HMAC of the code is stored.
/// Brute force: 6 digits, 5 tries per code, a new code at most once a minute, plus the strict per-IP rate limit.
public static class EmailOtp
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    public const int MaxAttempts = 5;
    public const string UnverifiedStatus = "Unverified";
    /// After this, someone else may register the same address (the unverified account loses it).
    public static readonly TimeSpan UnverifiedExpiry = TimeSpan.FromHours(24);

    /// Sets a fresh code on the user (caller saves) and returns it for sending.
    public static string Issue(User user, IEncryptionService encryption, DateTime now)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.EmailOtpHash = Hash(user.Id, code, encryption);
        user.EmailOtpSentAt = now;
        user.EmailOtpAttempts = 0;
        return code;
    }

    /// null = verified; otherwise the reason (attempt counted, caller saves either way).
    public static string? Check(User user, string? code, IEncryptionService encryption, DateTime now)
    {
        if (user.EmailOtpHash == null || user.EmailOtpSentAt is not { } sentAt || now - sentAt > Lifetime)
            return "Mã xác minh không đúng hoặc đã hết hạn.";
        if (user.EmailOtpAttempts >= MaxAttempts)
            return "Bạn đã nhập sai quá nhiều lần, vui lòng yêu cầu mã mới.";
        user.EmailOtpAttempts++;
        var expected = Encoding.ASCII.GetBytes(user.EmailOtpHash);
        var actual = Encoding.ASCII.GetBytes(Hash(user.Id, code?.Trim() ?? "", encryption));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return "Mã xác minh không đúng hoặc đã hết hạn.";

        user.EmailVerifiedAt = now;
        user.EmailOtpHash = null;
        user.EmailOtpSentAt = null;
        user.EmailOtpAttempts = 0;
        // Ownership proven: now the sign-up risk review (AutoReviewUsersService) picks the account up.
        if (user.Status == UnverifiedStatus) user.Status = "Pending";
        return null;
    }

    /// Sends the code; a mail failure is logged, not thrown: the account exists and the user can ask for a new code.
    public static async Task<bool> SendAsync(IEmailService email, ILogger logger, User user, string code)
    {
        try
        {
            await email.SendEmailAsync(user.Email!, "Mã xác minh email eBay Clone",
                $"<p>Mã xác minh của bạn là <b style=\"font-size:20px\">{code}</b>.</p>" +
                $"<p>Mã có hiệu lực {Lifetime.TotalMinutes:0} phút. Không chia sẻ mã này với bất kỳ ai.</p>");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Could not send the verification email of user {UserId}: {Error}", user.Id, ex.GetType().Name);
            return false;
        }
    }

    private static string Hash(int userId, string code, IEncryptionService encryption) =>
        encryption.ComputeHmac($"email-otp:{userId}:{code}");
}

public record VerifyEmailCommand(string Email, string Code) : IRequest<string?>;

public class VerifyEmailCommandHandler(IApplicationDbContext db, IEncryptionService encryption) : IRequestHandler<VerifyEmailCommand, string?>
{
    public async Task<string?> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Status == EmailOtp.UnverifiedStatus, cancellationToken);
        if (user == null) return "Mã xác minh không đúng hoặc đã hết hạn."; // same answer: does not reveal which emails exist
        var error = EmailOtp.Check(user, request.Code, encryption, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken); // also persists the failed attempt
        return error;
    }
}

public record ResendEmailCodeCommand(string Email) : IRequest;

public class ResendEmailCodeCommandHandler(IApplicationDbContext db, IEncryptionService encryption, IEmailService emailService,
    ILogger<ResendEmailCodeCommandHandler> logger) : IRequestHandler<ResendEmailCodeCommand>
{
    /// Always "done" to the caller: whether the email exists or is still unverified is not revealed.
    public async Task Handle(ResendEmailCodeCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Status == EmailOtp.UnverifiedStatus, cancellationToken);
        var now = DateTime.UtcNow;
        if (user == null || now - user.EmailOtpSentAt < EmailOtp.ResendCooldown) return;
        var code = EmailOtp.Issue(user, encryption, now);
        await db.SaveChangesAsync(cancellationToken);
        await EmailOtp.SendAsync(emailService, logger, user, code);
    }
}
