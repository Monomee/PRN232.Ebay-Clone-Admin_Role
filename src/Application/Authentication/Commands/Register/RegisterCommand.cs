using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.Authentication.Commands.Register;

/// Self sign-up for buyers and sellers. The account cannot do anything until:
///   1. the email code is entered (Status Unverified -> Pending),
///   2. the sign-up risk review links it against existing accounts (AutoReviewUsersService).
public record RegisterCommand : IRequest<RegisterResult>
{
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Role { get; init; } = "Buyer"; // Buyer | Seller
    public string? Phone { get; init; }
    // KYC: required for sellers, optional for buyers (all three together)
    public string? FullName { get; init; }
    public string? Cccd { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? CaptchaToken { get; init; }
    /// Set by the endpoint from the connection, never from the body.
    public string? RemoteIp { get; set; }
}

public record RegisterResult(int UserId, bool EmailSent);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        // A throwaway inbox makes the email code worthless: anyone can open a thousand of them.
        RuleFor(x => x.Email).Must(e => !AccountLinking.IsDisposableEmail(e)).WithMessage("Không chấp nhận email dùng một lần.");
        RuleFor(x => x.Username).NotEmpty().Length(3, 50).Matches("^[A-Za-z0-9_.-]+$")
            .WithMessage("Tên đăng nhập chỉ gồm chữ không dấu, số và . _ -");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(72) // bcrypt reads at most 72 bytes
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit)).WithMessage("Mật khẩu phải có ít nhất 8 ký tự, gồm cả chữ và số.");
        RuleFor(x => x.Role).Must(r => r is "Buyer" or "Seller").WithMessage("Chỉ đăng ký được tài khoản Buyer hoặc Seller.");
        RuleFor(x => x.Phone).Must(p => AccountLinking.NormalizePhone(p) != null).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Số điện thoại không hợp lệ.");

        const string kyc = "Seller (hoặc ai khai CCCD) phải khai đủ họ tên, số CCCD và ngày sinh.";
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100).When(NeedsKyc).WithMessage(kyc);
        RuleFor(x => x.DateOfBirth).NotNull().When(NeedsKyc).WithMessage(kyc);
        RuleFor(x => x.Cccd).NotEmpty().When(NeedsKyc).WithMessage(kyc);
        RuleFor(x => x.Cccd).Must((x, cccd) => CccdFormat.IsValid(cccd?.Trim(), x.DateOfBirth!.Value))
            .When(x => !string.IsNullOrWhiteSpace(x.Cccd) && x.DateOfBirth != null)
            .WithMessage("Số CCCD không hợp lệ (12 số, mã tỉnh và năm sinh phải khớp với ngày sinh).");
        RuleFor(x => x.DateOfBirth).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18))
            .When(x => x.Role == "Seller" && x.DateOfBirth != null).WithMessage("Seller phải đủ 18 tuổi.");
    }

    private static bool NeedsKyc(RegisterCommand x) =>
        x.Role == "Seller" || !string.IsNullOrWhiteSpace(x.Cccd) || !string.IsNullOrWhiteSpace(x.FullName) || x.DateOfBirth != null;
}

public class RegisterCommandHandler(IApplicationDbContext db, IEncryptionService encryption, ICaptchaVerifier captcha,
    IEmailService emailService, ILogger<RegisterCommandHandler> logger) : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (!await captcha.VerifyAsync(request.CaptchaToken, request.RemoteIp, cancellationToken))
            throw Fail("CaptchaToken", "Xác minh captcha không thành công, vui lòng thử lại.");

        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing != null)
        {
            // Anti-squatting: a sign-up that never proved the mailbox within UnverifiedExpiry gives the address back.
            // The old row is kept (renamed, still Unverified so it can never sign in) as risk-linking evidence.
            if (existing.Status != EmailOtp.UnverifiedStatus || existing.CreatedAt > now - EmailOtp.UnverifiedExpiry)
                throw Fail("Email", "Email đã được sử dụng.");
            existing.Email = $"released-{existing.Id}@invalid.local";
            existing.EmailOtpHash = null;
            existing.EmailOtpSentAt = null;
            await db.SaveChangesAsync(cancellationToken); // free the unique email before the new row is inserted
        }

        // eBay user IDs are unique: a copy of a reputable seller's name is how buyers get fooled.
        // Abandoned sign-ups (Unverified past UnverifiedExpiry) do not hold a name.
        // ponytail: app check only (no unique index, the seed data was never checked); two sign-ups racing can both pass.
        var username = request.Username.Trim();
        var abandoned = now - EmailOtp.UnverifiedExpiry;
        if (await db.Users.AnyAsync(u => u.Username == username && !(u.Status == EmailOtp.UnverifiedStatus && u.CreatedAt <= abandoned), cancellationToken))
            throw Fail("Username", "Tên đăng nhập đã được sử dụng.");

        var user = new User
        {
            Email = email,
            Username = username,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            Status = EmailOtp.UnverifiedStatus,
            ApprovalStatus = "PendingApproval",
            LastLoginIp = request.RemoteIp, // sign-up network: IP linking and datacenter check in the risk review
            CreatedAt = now,
        };
        if (!string.IsNullOrWhiteSpace(request.Phone))
            user.Addresses.Add(new Address { Phone = request.Phone.Trim(), FullName = request.FullName?.Trim(), IsDefault = true });
        if (!string.IsNullOrWhiteSpace(request.Cccd))
        {
            var cccd = request.Cccd.Trim();
            user.CccdLookupHash = encryption.ComputeHmac("cccd:" + cccd); // one account per citizen: checked by the risk review
            user.CCCD = encryption.Encrypt(cccd);
            // Self-declared, not proven (IsVerified stays false until a real eKYC check): the bank-holder name check uses it.
            user.VerificationDocuments = encryption.Encrypt(JsonSerializer.Serialize(new
            {
                fullName = request.FullName!.Trim(), cccd, dateOfBirth = request.DateOfBirth!.Value.ToString("yyyy-MM-dd"),
            }));
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken); // the code's hash includes the user id
        var code = EmailOtp.Issue(user, encryption, now);
        await db.SaveChangesAsync(cancellationToken);
        var sent = await EmailOtp.SendAsync(emailService, logger, user, code);
        return new RegisterResult(user.Id, sent);
    }

    private static ValidationException Fail(string field, string message) => new([new ValidationFailure(field, message)]);
}
