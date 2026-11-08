using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Users.Queries.GetUsers;

// Positional with defaults: [AsParameters] only treats a query value as optional when the constructor gives it a default
// (property initializers are ignored), so a missing ?pageNumber= falls back here instead of failing the request.
public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Tab = null, // All, PendingApproval, Banned, RiskReview (highest risk first)
    string? Status = null,
    string? ApprovalStatus = null,
    string? Role = null,
    string? Search = null,
    string? OrderBy = "Id",
    bool IsDescending = false) : IRequest<PaginatedList<UserBriefDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PaginatedList<UserBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(IApplicationDbContext context, IUser user, IIdentityService identityService)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
    }

    public async Task<PaginatedList<UserBriefDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<User> query = _context.Users.AsNoTracking();

        // 1. Role-based filtering based on requester's role
        var currentUserId = _user.Id;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var isSuperAdmin = await _identityService.IsInRoleAsync(currentUserId, Roles.SuperAdmin);
            
            if (!isSuperAdmin)
            {
                // If not SuperAdmin, restrict to Buyer and Seller roles only
                var allowedRoles = new[] { "Buyer", "Seller" };
                query = query.Where(u => u.Role != null && allowedRoles.Contains(u.Role));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Tab))
        {
            query = request.Tab.Trim().ToLowerInvariant() switch
            {
                "pendingapproval" or "pending" => query.Where(u => u.ApprovalStatus == "PendingApproval" || u.Status == "Pending"),
                "banned" => query.Where(u => u.Status == "Banned"),
                "riskreview" => query.Where(AccountLinking.AwaitingRiskReview),
                _ => query
            };
        }

        // Apply filters
        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(u => u.Status == request.Status);
        }

        if (!string.IsNullOrEmpty(request.ApprovalStatus))
        {
            query = query.Where(u => u.ApprovalStatus == request.ApprovalStatus);
        }

        if (!string.IsNullOrEmpty(request.Role))
        {
            query = query.Where(u => u.Role == request.Role);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchLower = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.Username != null && u.Username.ToLower().Contains(searchLower)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchLower)));
        }

        // Apply ordering
        query = request.OrderBy?.ToLower() switch
        {
            "username" => request.IsDescending ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username),
            "email" => request.IsDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "status" => request.IsDescending ? query.OrderByDescending(u => u.Status) : query.OrderBy(u => u.Status),
            "lastloginat" => request.IsDescending ? query.OrderByDescending(u => u.LastLoginAt) : query.OrderBy(u => u.LastLoginAt),
            _ => request.IsDescending ? query.OrderByDescending(u => u.Id) : query.OrderBy(u => u.Id)
        };

        if (string.Equals(request.Tab?.Trim(), "RiskReview", StringComparison.OrdinalIgnoreCase))
            query = query.OrderByDescending(u => u.RiskScore).ThenBy(u => u.Id);

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(UserMappings.Projection)
            .ToListAsync(cancellationToken);

        return new PaginatedList<UserBriefDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
