using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Withdrawals.Queries.GetWithdrawalRequests;

public record WithdrawalRequestDto
{
    public int Id { get; init; }
    public int SellerId { get; init; }
    public string SellerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? BankAccountName { get; init; }
}

public record GetWithdrawalRequestsQuery(string? Status) : IRequest<List<WithdrawalRequestDto>>;

public class GetWithdrawalRequestsQueryHandler : IRequestHandler<GetWithdrawalRequestsQuery, List<WithdrawalRequestDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWithdrawalRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WithdrawalRequestDto>> Handle(GetWithdrawalRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.WithdrawalRequests
            .Include(w => w.Seller)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(w => w.Status == request.Status);
        }

        return await query
            .Select(w => new WithdrawalRequestDto
            {
                Id = w.Id,
                SellerId = w.SellerId,
                SellerName = w.Seller!.Username ?? "Unknown",
                Amount = w.Amount,
                Status = w.Status,
                RequestedAt = w.RequestedAt,
                BankName = w.BankName,
                BankAccountNumber = w.BankAccountNumber,
                BankAccountName = w.BankAccountName
            })
            .OrderByDescending(w => w.RequestedAt)
            .ToListAsync(cancellationToken);
    }
}
