using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Payouts.Commands.ReleaseHold;

/// <summary>
/// Admin releases a "Hold" PayoutTransaction so it will be retried on the next engine run.
/// </summary>
public record ReleaseHoldCommand(int PayoutTransactionId) : IRequest<bool>;

public class ReleaseHoldCommandHandler : IRequestHandler<ReleaseHoldCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ReleaseHoldCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ReleaseHoldCommand request, CancellationToken cancellationToken)
    {
        var tx = await _context.PayoutTransactions
            .FirstOrDefaultAsync(t => t.Id == request.PayoutTransactionId, cancellationToken);

        if (tx == null) return false;

        // Only Hold or Failed transactions can be released
        if (tx.Status != PayoutTransaction.StatusHold && tx.Status != PayoutTransaction.StatusFailed)
            return false;

        // Mark as Released — the next engine run will re-evaluate this seller from scratch
        tx.Status = PayoutTransaction.StatusReleased;
        tx.ErrorLog = $"[Released by Admin at {DateTime.UtcNow:o}] Previous: {tx.ErrorLog}";
        tx.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
