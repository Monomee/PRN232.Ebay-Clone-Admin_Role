using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using FluentValidation.Results;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.Users.Commands.UpdateBankAccount;

/// Seller changes the bank account payouts go to. The classic account-takeover cash-out: sign in, switch the bank
/// account, withdraw. So the holder must be the verified owner, and the first payouts wait 48h while the owner is told.
public record UpdateBankAccountCommand(string BankName, string AccountNumber, string AccountName) : IRequest;

public class UpdateBankAccountCommandHandler(IApplicationDbContext db, IUser actor, IEncryptionService encryption, ISensitiveDataLogger log)
    : IRequestHandler<UpdateBankAccountCommand>
{
    public static readonly TimeSpan PayoutHoldAfterChange = TimeSpan.FromHours(48);

    public async Task Handle(UpdateBankAccountCommand request, CancellationToken ct)
    {
        var userId = actor.GetRequiredActorId();
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

        if (string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.AccountName))
            throw Fail("BankName", "Vui lòng nhập tên ngân hàng và tên chủ tài khoản.");
        if (AccountLinking.NormalizeBankAccount(request.AccountNumber) == null)
            throw Fail("AccountNumber", "Số tài khoản không hợp lệ.");
        if (AccountLinking.KycName(user.VerificationDocuments, encryption) is { } kycName && !AccountLinking.NamesMatch(kycName, request.AccountName))
            throw Fail("AccountName", "Tên chủ tài khoản phải trùng với tên trên giấy tờ đã xác minh.");

        var now = DateTime.UtcNow;
        var hadBank = !string.IsNullOrEmpty(user.BankAccountMock);
        user.BankAccountMock = encryption.Encrypt(JsonSerializer.Serialize(new
        {
            bankName = request.BankName.Trim(), accountNumber = request.AccountNumber.Trim(), accountName = request.AccountName.Trim(),
        }));
        if (hadBank && !(user.PayoutHoldUntil > now + PayoutHoldAfterChange)) user.PayoutHoldUntil = now + PayoutHoldAfterChange;

        db.Notifications.Add(new Notification
        {
            UserId = userId, Title = "Payout bank account changed",
            Content = hadBank
                ? "Your payout bank account was changed. Payouts are paused for 48 hours. If this was not you, contact support now."
                : "A payout bank account was added to your account.",
            Type = "InApp", Status = "Sent", SentAt = now, CreatedAt = now,
        });
        var last4 = new string(request.AccountNumber.Where(char.IsLetterOrDigit).ToArray())[^4..];
        log.Add("UpdatePaymentInfo", "User", userId.ToString(), "Financial", new { request.BankName, accountLast4 = last4, replaced = hadBank });
        await db.SaveChangesAsync(ct);

        // Link the new account right away (shared with a banned seller?) instead of waiting for the 10-minute sync.
        // The bank change is already saved: if this races the background sync (unique index), that sync covers it.
        try { await AccountLinking.SyncIdentifiersAsync(db, encryption, db.Users.Where(u => u.Id == userId), ct); }
        catch (DbUpdateException) { }
    }

    private static ValidationException Fail(string field, string message) => new([new ValidationFailure(field, message)]);
}
