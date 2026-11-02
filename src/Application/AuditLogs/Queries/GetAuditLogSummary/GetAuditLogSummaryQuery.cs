using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.AuditLogs.Queries.GetAuditLogSummary;

/// Audit Logs cards over all entries. Critical = delete or ban actions (the page's existing definition).
public record AuditLogSummaryDto(int Total, int Critical);

public record GetAuditLogSummaryQuery : IRequest<AuditLogSummaryDto>;

public class GetAuditLogSummaryQueryHandler : IRequestHandler<GetAuditLogSummaryQuery, AuditLogSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AuditLogSummaryDto> Handle(GetAuditLogSummaryQuery request, CancellationToken cancellationToken)
    {
        return new AuditLogSummaryDto(
            await _context.AdminActions.CountAsync(cancellationToken),
            await _context.AdminActions.CountAsync(a => a.Action.ToLower().Contains("delete") || a.Action.ToLower().Contains("ban"), cancellationToken));
    }
}
