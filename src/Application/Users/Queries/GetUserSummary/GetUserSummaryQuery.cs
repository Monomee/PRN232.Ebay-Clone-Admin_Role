using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Users.Queries.GetUserSummary;

/// Totals for the Users page cards, over all users rather than the loaded page.
public record UserSummaryDto(int Total, int PendingApproval, int Banned);

public record GetUserSummaryQuery : IRequest<UserSummaryDto>;

public class GetUserSummaryQueryHandler : IRequestHandler<GetUserSummaryQuery, UserSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetUserSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<UserSummaryDto> Handle(GetUserSummaryQuery request, CancellationToken cancellationToken)
    {
        // Same conditions as the PendingApproval and Banned tabs in GetUsersQuery
        return new UserSummaryDto(
            await _context.Users.CountAsync(cancellationToken),
            await _context.Users.CountAsync(u => u.ApprovalStatus == "PendingApproval" || u.Status == "Pending", cancellationToken),
            await _context.Users.CountAsync(u => u.Status == "Banned", cancellationToken));
    }
}
