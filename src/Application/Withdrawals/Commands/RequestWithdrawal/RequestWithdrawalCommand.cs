using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using FluentValidation.Results;
using MediatR;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.Withdrawals.Commands.RequestWithdrawal;

public record RequestWithdrawalCommand : IRequest<int>
{
    public required decimal Amount { get; init; }
    public required string BankName { get; init; }
    public required string BankAccountNumber { get; init; }
    public required string BankAccountName { get; init; }
}

public class RequestWithdrawalCommandHandler : IRequestHandler<RequestWithdrawalCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IEncryptionService _encryption;
    private readonly ISensitiveDataLogger _sensitiveLog;

    public RequestWithdrawalCommandHandler(IApplicationDbContext context, IUser user, IEncryptionService encryption, ISensitiveDataLogger sensitiveLog)
    {
        _context = context;
        _user = user;
        _encryption = encryption;
        _sensitiveLog = sensitiveLog;
    }

    public async Task<int> Handle(RequestWithdrawalCommand request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(_user.Id, out var sellerId) || sellerId <= 0) throw new UnauthorizedAccessException();

        // Same rules as the payout engine, or a withdrawal becomes the way around them.
        var seller = await _context.Users.FindAsync(new object[] { sellerId }, cancellationToken);
        AccountStanding.Ensure(seller, "Seller");
        if (seller!.PayoutHoldUntil > DateTime.UtcNow)
            throw Fail($"Tài khoản đang bị tạm giữ thanh toán đến {seller.PayoutHoldUntil:dd/MM/yyyy HH:mm} (UTC).");
        // Only to the registered account: it was checked against the KYC name, is linked to other accounts by
        // number, and changing it starts a 48h hold. A free-text account in the request would skip all three.
        var registered = AccountLinking.BankAccountNumber(seller.BankAccountMock, _encryption);
        if (registered == null)
            throw Fail("Bạn cần đăng ký tài khoản ngân hàng trước khi rút tiền.");
        if (AccountLinking.NormalizeBankAccount(request.BankAccountNumber) != registered)
            throw Fail("Chỉ được rút về tài khoản ngân hàng đã đăng ký.");

        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == sellerId, cancellationToken);
            
        if (wallet == null)
        {
             // Create wallet if not exists (should ideally be created on user registration)
             wallet = new SellerWallet { SellerId = sellerId };
             _context.SellerWallets.Add(wallet);
        }

        // 1. Check logical balance (AvailableBalance >= Amount)
        // Note: The wallet.LockAvailableFunds method checks this and moves funds to LockedBalance.
        
        wallet.LockAvailableFunds(request.Amount); // Throw exception if insufficient

        var withdrawal = new WithdrawalRequest
        {
            SellerId = sellerId,
            Amount = request.Amount,
            BankName = _encryption.Encrypt(request.BankName),
            BankAccountNumber = _encryption.Encrypt(request.BankAccountNumber),
            BankAccountName = _encryption.Encrypt(AccountLinking.BankHolderName(seller.BankAccountMock, _encryption) ?? request.BankAccountName),
            Status = WithdrawalRequest.StatusPending, 
            RequestedAt = DateTime.UtcNow
        };

        _context.WithdrawalRequests.Add(withdrawal);
        _sensitiveLog.Add("RequestWithdrawal", "Seller", sellerId.ToString(), "Financial",
            new { sellerId, request.Amount, withdrawal.RequestedAt, request.BankName, request.BankAccountNumber, request.BankAccountName });
        
        await _context.SaveChangesAsync(cancellationToken);

        return withdrawal.Id;
    }

    private static ValidationException Fail(string message) => new([new ValidationFailure("BankAccount", message)]);
}
