using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Financials.Queries.GetSellerWallets;

public record SellerWalletDto
{
    public int Id { get; init; }
    public int SellerId { get; init; }
    public string SellerName { get; init; } = string.Empty;
    public decimal PendingBalance { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal LockedBalance { get; init; }
    public decimal TotalEarnings { get; init; }
    public decimal TotalWithdrawn { get; init; }
}

public record GetSellerWalletsQuery : IRequest<List<SellerWalletDto>>;

public class GetSellerWalletsQueryHandler : IRequestHandler<GetSellerWalletsQuery, List<SellerWalletDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSellerWalletsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SellerWalletDto>> Handle(GetSellerWalletsQuery request, CancellationToken cancellationToken)
    {
        return await _context.SellerWallets
            .Include(w => w.Seller)
            .Select(w => new SellerWalletDto
            {
                Id = w.Id,
                SellerId = w.SellerId,
                SellerName = w.Seller!.Username ?? "Unknown",
                PendingBalance = w.PendingBalance,
                AvailableBalance = w.AvailableBalance,
                LockedBalance = w.LockedBalance,
                TotalEarnings = w.TotalEarnings,
                TotalWithdrawn = w.TotalWithdrawn
            })
            .ToListAsync(cancellationToken);
    }
}
