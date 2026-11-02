using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Products.Queries.GetManagedProductSummary;

/// Product moderation cards: products with a pending report, and products whose reports were resolved or rejected.
public record ManagedProductSummaryDto(int Reported, int Resolved, int Rejected);

public record GetManagedProductSummaryQuery : IRequest<ManagedProductSummaryDto>;

public class GetManagedProductSummaryQueryHandler : IRequestHandler<GetManagedProductSummaryQuery, ManagedProductSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetManagedProductSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ManagedProductSummaryDto> Handle(GetManagedProductSummaryQuery request, CancellationToken cancellationToken)
    {
        // Report statuses are set by ResolveProductViolation: Pending -> Resolved (hide/delete) or Rejected
        Task<int> Count(string status) => _context.Products.CountAsync(p => p.Reports.Any(r => r.Status == status), cancellationToken);

        return new ManagedProductSummaryDto(await Count("Pending"), await Count("Resolved"), await Count("Rejected"));
    }
}
