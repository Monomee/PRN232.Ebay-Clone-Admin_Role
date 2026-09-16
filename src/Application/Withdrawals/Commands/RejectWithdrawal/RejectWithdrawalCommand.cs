using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;

namespace EbayClone.Application.Withdrawals.Commands.RejectWithdrawal;

public record RejectWithdrawalCommand(int WithdrawalId, string Reason) : IRequest;

public class RejectWithdrawalCommandHandler : IRequestHandler<RejectWithdrawalCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RejectWithdrawalCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(RejectWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var withdrawal = await _context.WithdrawalRequests
            .FirstOrDefaultAsync(w => w.Id == request.WithdrawalId, cancellationToken);

        if (withdrawal == null) throw new KeyNotFoundException($"Withdrawal request {request.WithdrawalId} not found.");

        if (withdrawal.Status != WithdrawalRequest.StatusPending)
        {
            throw new InvalidOperationException("Only pending requests can be rejected.");
        }

        // 1. Find Wallet
        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == withdrawal.SellerId, cancellationToken);

        if (wallet == null) throw new InvalidOperationException("Seller wallet not found.");

        // 2. Refund to Available Balance (from LockedBalance)
        wallet.UnlockFunds(withdrawal.Amount);

        // 3. Update Status
        withdrawal.Status = WithdrawalRequest.StatusRejected;
        withdrawal.ProcessedBy = int.Parse(_user.Id ?? "0");
        withdrawal.ProcessedAt = DateTime.UtcNow;
        withdrawal.RejectionReason = request.Reason;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
