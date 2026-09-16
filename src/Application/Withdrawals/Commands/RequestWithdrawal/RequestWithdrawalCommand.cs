using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;

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

    public RequestWithdrawalCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<int> Handle(RequestWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var sellerId = int.Parse(_user.Id ?? "0"); // Assume authenticated
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
            BankName = request.BankName,
            BankAccountNumber = request.BankAccountNumber,
            BankAccountName = request.BankAccountName,
            Status = WithdrawalRequest.StatusPending, 
            RequestedAt = DateTime.UtcNow
        };

        _context.WithdrawalRequests.Add(withdrawal);
        
        await _context.SaveChangesAsync(cancellationToken);

        return withdrawal.Id;
    }
}
