using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;

namespace EbayClone.Application.Withdrawals.Commands.ApproveWithdrawal;

public record ApproveWithdrawalCommand(int WithdrawalId, string TransactionId) : IRequest;

public class ApproveWithdrawalCommandHandler : IRequestHandler<ApproveWithdrawalCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ApproveWithdrawalCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(ApproveWithdrawalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var withdrawal = await _context.WithdrawalRequests
                .FirstOrDefaultAsync(w => w.Id == request.WithdrawalId, cancellationToken);

            if (withdrawal == null) throw new KeyNotFoundException($"Withdrawal request {request.WithdrawalId} not found.");

            if (withdrawal.Status != WithdrawalRequest.StatusPending)
            {
                throw new InvalidOperationException("Only pending requests can be approved.");
            }

            var wallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == withdrawal.SellerId, cancellationToken);
            
            if (wallet == null) throw new InvalidOperationException("Seller wallet not found.");
            
            wallet.ConfirmWithdrawal(withdrawal.Amount);

            withdrawal.Status = WithdrawalRequest.StatusApproved;
            withdrawal.ProcessedBy = int.Parse(_user.Id ?? "0");
            withdrawal.ProcessedAt = DateTime.UtcNow;
            withdrawal.TransactionId = request.TransactionId;

            var transaction = new FinancialTransaction
            {
                SellerId = withdrawal.SellerId,
                UserId = withdrawal.SellerId, 
                Type = "Withdrawal",
                Amount = -withdrawal.Amount,
                BalanceAfter = wallet.AvailableBalance,
                WithdrawalId = withdrawal.Id,
                Description = string.IsNullOrEmpty(request.TransactionId) 
                    ? "Withdrawal Approved" 
                    : $"Withdrawal Approved: {request.TransactionId}",
                Date = DateTime.UtcNow
            };
            _context.FinancialTransactions.Add(transaction);

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Emergency debug logging - Try multiple paths to be sure
            try { await System.IO.File.WriteAllTextAsync("approve_error.txt", ex.ToString()); } catch {}
            try { await System.IO.File.WriteAllTextAsync("C:\\temp\\approve_error.txt", ex.ToString()); } catch {}
            throw;
        }
    }
}
