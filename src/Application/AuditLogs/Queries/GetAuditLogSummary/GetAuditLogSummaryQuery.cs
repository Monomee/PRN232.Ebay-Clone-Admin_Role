using EbayClone.Application.AuditLogs.Queries.GetAuditLogs;
using EbayClone.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.AuditLogs.Queries.GetAuditLogSummary;

/// Audit Logs cards over every entry matching the list's filter. Critical = delete or ban actions, not unban (z8rp3fcgjm).
public record AuditLogSummaryDto(int Total, int Critical);

public record GetAuditLogSummaryQuery(GetAuditLogsQuery Filter) : IRequest<AuditLogSummaryDto>;

public class GetAuditLogSummaryQueryHandler : IRequestHandler<GetAuditLogSummaryQuery, AuditLogSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AuditLogSummaryDto> Handle(GetAuditLogSummaryQuery request, CancellationToken cancellationToken)
    {
        var logs = GetAuditLogsQueryHandler.ApplyFilters(_context.AdminActions, request.Filter);
        return new AuditLogSummaryDto(
            await logs.CountAsync(cancellationToken),
            await logs.CountAsync(a => a.Action.ToLower().Contains("delete") ||
                (a.Action.ToLower().Contains("ban") && !a.Action.ToLower().Contains("unban")), cancellationToken));
    }
}
