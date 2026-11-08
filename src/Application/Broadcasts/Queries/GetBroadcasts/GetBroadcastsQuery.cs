using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Broadcasts.Queries.GetBroadcasts;

// Positional with defaults: [AsParameters] only treats a query value as optional when the constructor gives it a default
// (property initializers are ignored), so a missing ?pageNumber= falls back here instead of failing the request.
public record GetBroadcastsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null, // Pending, Sent, Scheduled
    string? Type = null) : IRequest<PaginatedList<BroadcastDto>>;

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
