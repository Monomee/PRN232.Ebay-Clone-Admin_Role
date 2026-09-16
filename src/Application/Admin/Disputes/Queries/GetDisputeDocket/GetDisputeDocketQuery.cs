using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Admin.Disputes.Queries.GetDisputeDocket;

public record GetDisputeDocketQuery : IRequest<PaginatedList<DisputeDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    
    // Filters
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Type { get; init; }
    public int? AssignedTo { get; init; }
    public bool? OnlyMyDisputes { get; init; }
    public bool? OnlyUrgent { get; init; }
    public string? SearchTerm { get; init; }
    
    // Sorting
    public string SortBy { get; init; } = "Deadline"; // Deadline, Amount, Priority, CreatedAt
    public bool Descending { get; init; } = false;
}

public class GetDisputeDocketQueryHandler : IRequestHandler<GetDisputeDocketQuery, PaginatedList<DisputeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;

    public GetDisputeDocketQueryHandler(IApplicationDbContext context, IUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedList<DisputeDto>> Handle(GetDisputeDocketQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Disputes
            .Include(d => d.Order)
                .ThenInclude(o => o!.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(d => d.RaisedByNavigation)
            .AsQueryable();

        // FILTERS
        
        // Status filter
        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(d => d.Status == request.Status);
        }
        else
        {
            // Default: Show only active disputes (not closed)
            query = query.Where(d => d.Status != DisputeStatuses.Closed);
        }

        // Priority filter
        if (!string.IsNullOrEmpty(request.Priority))
        {
            query = query.Where(d => d.Priority == request.Priority);
        }

        // Type filter
        if (!string.IsNullOrEmpty(request.Type))
        {
            query = query.Where(d => d.Type == request.Type);
        }

        // Assignment filter
        if (request.AssignedTo.HasValue)
        {
            query = query.Where(d => d.AssignedTo == request.AssignedTo.Value);
        }

        // Only my disputes
        if (request.OnlyMyDisputes == true && !string.IsNullOrEmpty(_currentUser.Id))
        {
            if (int.TryParse(_currentUser.Id, out int currentAdminId))
            {
                query = query.Where(d => d.AssignedTo == currentAdminId);
            }
        }

        // Only urgent (deadline < 24h)
        if (request.OnlyUrgent == true)
        {
            var urgentDeadline = DateTime.UtcNow.AddHours(24);
            query = query.Where(d => d.Deadline != null && d.Deadline <= urgentDeadline);
        }

        // Search
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(d =>
                (d.CaseId != null && d.CaseId.ToLower().Contains(searchLower)) ||
                (d.Description != null && d.Description.ToLower().Contains(searchLower)) ||
                (d.RaisedByNavigation != null && d.RaisedByNavigation.Username != null && 
                 d.RaisedByNavigation.Username.ToLower().Contains(searchLower))
            );
        }

        // SORTING
        query = request.SortBy.ToLower() switch
        {
            "amount" => request.Descending 
                ? query.OrderByDescending(d => d.Amount) 
                : query.OrderBy(d => d.Amount),
            
            "priority" => request.Descending
                ? query.OrderByDescending(d => d.Priority)
                : query.OrderBy(d => d.Priority),
            
            "createdat" => request.Descending
                ? query.OrderByDescending(d => d.CreatedAt)
                : query.OrderBy(d => d.CreatedAt),
            
            "deadline" or _ => request.Descending
                ? query.OrderByDescending(d => d.Deadline)
                : query.OrderBy(d => d.Deadline)
        };

        // PROJECT TO DTO
        var disputeDtos = query.Select(d => new DisputeDto
        {
            Id = d.Id,
            CaseId = d.CaseId ?? string.Empty,
            OrderId = d.OrderId,
            OrderNumber = d.Order != null ? $"#{d.Order.Id}" : string.Empty,
            Type = d.Type,
            Subcategory = d.Subcategory,
            Status = d.Status,
            Priority = d.Priority,
            Amount = d.Amount,
            Deadline = d.Deadline,
            Description = d.Description,
            DesiredOutcome = d.DesiredOutcome,
            
            // Buyer info
            BuyerId = d.RaisedBy,
            BuyerUsername = d.RaisedByNavigation != null ? d.RaisedByNavigation.Username : null,
            BuyerEmail = d.RaisedByNavigation != null ? d.RaisedByNavigation.Email : null,
            
            // Seller info (from order)
            SellerId = d.Order != null && d.Order.OrderItems.Any() 
                ? d.Order.OrderItems.First().Product!.SellerId 
                : null,
            SellerUsername = d.Order != null && d.Order.OrderItems.Any() 
                && d.Order.OrderItems.First().Product != null
                && d.Order.OrderItems.First().Product!.Seller != null
                ? d.Order.OrderItems.First().Product!.Seller!.Username
                : null,
            
            // Product info
            ProductTitle = d.Order != null && d.Order.OrderItems.Any() 
                && d.Order.OrderItems.First().Product != null
                ? d.Order.OrderItems.First().Product!.Title
                : null,
            ProductImage = d.Order != null && d.Order.OrderItems.Any() 
                && d.Order.OrderItems.First().Product != null
                ? d.Order.OrderItems.First().Product!.Images
                : null,
            ProductPrice = d.Order != null && d.Order.OrderItems.Any() 
                && d.Order.OrderItems.First().Product != null
                ? d.Order.OrderItems.First().Product!.Price
                : null,
            
            // Timeline
            CreatedAt = d.CreatedAt,
            EscalatedAt = d.EscalatedAt,
            ResolvedAt = d.ResolvedAt,
            
            // Assignment
            AssignedTo = d.AssignedTo,
            
            // Resolution
            Winner = d.Winner,
            RefundAmount = d.RefundAmount
        });

        return await PaginatedList<DisputeDto>.CreateAsync(disputeDtos, request.PageNumber, request.PageSize);
    }
}
