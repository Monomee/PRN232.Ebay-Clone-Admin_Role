using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Payouts.Commands.RunPayoutEngine;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Sellers.Commands.PushPayout;

public record PushPayoutCommand : IRequest<PayoutEngineResult>
{
    public int SellerId { get; init; }
    public decimal Amount { get; init; }
}

public class PushPayoutCommandHandler : IRequestHandler<PushPayoutCommand, PayoutEngineResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public PushPayoutCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<PayoutEngineResult> Handle(PushPayoutCommand request, CancellationToken cancellationToken)
    {
        var seller = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.SellerId, cancellationToken);

        if (seller == null)
            throw new ArgumentException($"Seller with ID '{request.SellerId}' not found.");

        // 1. Ensure Bank Account is linked
        if (string.IsNullOrEmpty(seller.BankAccountMock))
        {
            seller.BankAccountMock = "{\"bankName\": \"Manual Test Bank\", \"accountNumber\": \"TEST-999\", \"accountName\": \"" + seller.Username + "\"}";
        }

        // 2. Inject Available Balance if needed
        var wallet = await _context.SellerWallets.FirstOrDefaultAsync(w => w.SellerId == seller.Id, cancellationToken);
        if (wallet == null)
        {
            wallet = new SellerWallet { SellerId = seller.Id, AvailableBalance = 0, PendingBalance = 0, LockedBalance = 0 };
            _context.SellerWallets.Add(wallet);
        }

        if (wallet.AvailableBalance < request.Amount)
        {
            throw new ArgumentException($"Insufficient Available Balance. Requested: {request.Amount:N0} VND, Available: {wallet.AvailableBalance:N0} VND.");
        }

        // 3. Trigger targeted Payout Engine run with specific amount
        Console.WriteLine($"[PUSH_DEBUG] Triggering RunPayoutEngine for Seller {seller.Id} with Amount {request.Amount}");
        return await _sender.Send(new RunPayoutEngineCommand(seller.Id, request.Amount), cancellationToken);
    }
}
