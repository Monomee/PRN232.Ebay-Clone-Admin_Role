using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.SensitiveLogs.Queries.GetSensitiveLogs;

public record GetSensitiveLogsQuery : IRequest<PaginatedList<SensitiveLogDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? ActorUsername { get; init; }
    public string? Action { get; init; }
    public string? DataClassification { get; init; } // 'All', 'PII', 'Financial', 'SecurityCredential', 'AdminPrivilege'
    public string? SearchTerm { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public class GetSensitiveLogsQueryHandler : IRequestHandler<GetSensitiveLogsQuery, PaginatedList<SensitiveLogDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSensitiveLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<SensitiveLogDto>> Handle(GetSensitiveLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SensitiveDataLogs
            .AsNoTracking()
            .AsQueryable();

        // 1. Classification filter
        if (!string.IsNullOrWhiteSpace(request.DataClassification) && !request.DataClassification.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.DataClassification == request.DataClassification);
        }

        // 2. Action filter
        if (!string.IsNullOrWhiteSpace(request.Action) && !request.Action.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.Action.Contains(request.Action));
        }

        // 3. Actor Username
        if (!string.IsNullOrWhiteSpace(request.ActorUsername))
        {
            var userTerm = request.ActorUsername.Trim().ToLower();
            query = query.Where(l => l.ActorUsername != null && l.ActorUsername.ToLower().Contains(userTerm));
        }

        // 4. General Search (TargetEntity, TargetId, Action, Actor)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(l =>
                (l.Action != null && l.Action.ToLower().Contains(term)) ||
                (l.TargetEntity != null && l.TargetEntity.ToLower().Contains(term)) ||
                (l.TargetId != null && l.TargetId.ToLower().Contains(term)) ||
                (l.ActorUsername != null && l.ActorUsername.ToLower().Contains(term)) ||
                (l.IpAddress != null && l.IpAddress.ToLower().Contains(term))
            );
        }

        // 5. Date filters
        if (request.FromDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= request.ToDate.Value);
        }

        var projected = query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new SensitiveLogDto
            {
                Id = l.Id,
                ActorId = l.ActorId,
                ActorUsername = l.ActorUsername,
                ActorRole = l.ActorRole,
                Action = l.Action,
                TargetEntity = l.TargetEntity,
                TargetId = l.TargetId,
                DataClassification = l.DataClassification,
                EncryptedData = l.EncryptedData,
                EncryptionAlgorithm = l.EncryptionAlgorithm,
                KeyIdentifier = l.KeyIdentifier,
                DataHash = l.DataHash,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                CreatedAt = l.CreatedAt
            });

        return await PaginatedList<SensitiveLogDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
