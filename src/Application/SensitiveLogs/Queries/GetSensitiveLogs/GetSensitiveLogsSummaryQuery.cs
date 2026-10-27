using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.SensitiveLogs.Queries.GetSensitiveLogs;

public record GetSensitiveLogsSummaryQuery : IRequest<SensitiveLogsSummaryDto>;

public class GetSensitiveLogsSummaryQueryHandler : IRequestHandler<GetSensitiveLogsSummaryQuery, SensitiveLogsSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetSensitiveLogsSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SensitiveLogsSummaryDto> Handle(GetSensitiveLogsSummaryQuery request, CancellationToken cancellationToken)
    {
        var logs = await _context.SensitiveDataLogs.AsNoTracking().ToListAsync(cancellationToken);

        return new SensitiveLogsSummaryDto
        {
            TotalLogs = logs.Count,
            PiiCount = logs.Count(l => l.DataClassification == "PII"),
            FinancialCount = logs.Count(l => l.DataClassification == "Financial"),
            SecurityCredentialCount = logs.Count(l => l.DataClassification == "SecurityCredential"),
            AdminPrivilegeCount = logs.Count(l => l.DataClassification == "AdminPrivilege")
        };
    }
}
