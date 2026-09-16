using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EbayClone.Application.AuditLogs.Queries.GetAuditLogs;

public record GetAuditLogsQuery : IRequest<PaginatedList<AuditLogDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int? AdminId { get; init; }
    public string? AdminUsername { get; init; }
    public string? TargetType { get; init; }
    public int? TargetId { get; init; }
    public string? Action { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedList<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AdminActions
            .Include(a => a.Admin)
            .AsQueryable();

        // Filter by admin
        if (request.AdminId.HasValue)
        {
            query = query.Where(a => a.AdminId == request.AdminId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.AdminUsername))
        {
            var adminUsernameLower = request.AdminUsername.Trim().ToLower();
            query = query.Where(a => a.Admin != null &&
                a.Admin.Username != null &&
                a.Admin.Username.ToLower().Contains(adminUsernameLower));
        }

        // Filter by target type
        if (!string.IsNullOrEmpty(request.TargetType))
        {
            query = query.Where(a => a.TargetType == request.TargetType);
        }

        if (request.TargetId.HasValue)
        {
            query = query.Where(a => a.TargetId == request.TargetId.Value);
        }

        // Filter by action
        if (!string.IsNullOrEmpty(request.Action))
        {
            query = query.Where(a => a.Action.Contains(request.Action));
        }

        // Filter by date range
        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new
            {
                Id = a.Id,
                AdminId = a.AdminId,
                AdminUsername = a.Admin != null ? a.Admin.Username : null,
                Action = a.Action,
                TargetType = a.TargetType,
                TargetId = a.TargetId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(a => new AuditLogDto
        {
            Id = a.Id,
            AdminId = a.AdminId,
            AdminUsername = a.AdminUsername,
            Action = a.Action,
            TargetType = a.TargetType,
            TargetId = a.TargetId,
            Details = a.Details,
            BeforeData = ExtractJsonProperty(a.Details, "before"),
            AfterData = ExtractJsonProperty(a.Details, "after"),
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt
        }).ToList();

        return new PaginatedList<AuditLogDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private static string? ExtractJsonProperty(string? details, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(details);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.GetRawText();
        }
        catch
        {
            return null;
        }
    }
}
