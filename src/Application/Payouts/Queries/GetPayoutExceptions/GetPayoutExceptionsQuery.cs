using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Payouts.Queries.GetPayoutExceptions;

public record GetPayoutExceptionsQuery : IRequest<List<PayoutExceptionDto>>;

public record PayoutExceptionDto(
    int Id,
    int SellerId,
    string? SellerUsername,
    decimal Amount,
    string Status,
    string? ErrorLog,
    string? BankSnapshot,
    string? SessionId,
    DateTime CreatedAt
);

public class GetPayoutExceptionsQueryHandler : IRequestHandler<GetPayoutExceptionsQuery, List<PayoutExceptionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPayoutExceptionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PayoutExceptionDto>> Handle(GetPayoutExceptionsQuery request, CancellationToken cancellationToken)
    {
        return await _context.PayoutTransactions
            .Include(t => t.Seller)
            .Where(t => t.Status == "Failed" || t.Status == "Hold")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PayoutExceptionDto(
                t.Id,
                t.SellerId,
                t.Seller != null ? t.Seller.Username : null,
                t.Amount,
                t.Status,
                t.ErrorLog,
                t.BankSnapshot,
                t.SessionId,
                t.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
