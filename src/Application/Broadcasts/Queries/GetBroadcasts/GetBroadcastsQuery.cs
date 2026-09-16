using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Broadcasts.Queries.GetBroadcasts;

public record GetBroadcastsQuery : IRequest<PaginatedList<BroadcastDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Status { get; init; } // Pending, Sent, Scheduled
    public string? Type { get; init; } // Email, InApp, SMS
}

public class GetBroadcastsQueryHandler : IRequestHandler<GetBroadcastsQuery, PaginatedList<BroadcastDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBroadcastsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<BroadcastDto>> Handle(GetBroadcastsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications
            .Include(n => n.Creator)
            .Where(n => n.UserId == null) // Broadcasts only (not individual notifications)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(n => n.Status == request.Status);
        }

        if (!string.IsNullOrEmpty(request.Type))
        {
            query = query.Where(n => n.Type == request.Type);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new BroadcastDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                UserRole = n.UserRole,
                Type = n.Type,
                Status = n.Status,
                ScheduledAt = n.ScheduledAt,
                SentAt = n.SentAt,
                CreatedBy = n.CreatedBy,
                CreatedByUsername = n.Creator != null ? n.Creator.Username : null,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<BroadcastDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
