using System.Text.Json;
using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Admin.Disputes.Queries.GetDisputeDetail;

public record GetDisputeDetailQuery(int DisputeId) : IRequest<DisputeDetailDto>;

public class GetDisputeDetailQueryHandler : IRequestHandler<GetDisputeDetailQuery, DisputeDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetDisputeDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeDetailDto> Handle(GetDisputeDetailQuery request, CancellationToken cancellationToken)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Order)
                .ThenInclude(o => o!.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p!.Seller)
            .Include(d => d.RaisedByNavigation)
            .Include(d => d.Messages)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

        Guard.Against.NotFound(request.DisputeId, dispute);

        // Update view count
        dispute.ViewCount++;
        dispute.LastViewedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Parse evidence from JSON
        var buyerEvidences = ParseEvidence(dispute.BuyerEvidence);
        var sellerEvidences = ParseEvidence(dispute.SellerEvidence);
        var offerHistory = ParseOfferHistory(dispute.OfferHistory);

        // Build timeline
        var timeline = BuildTimeline(dispute);

        // Get buyer stats
        var buyerId = dispute.Order?.BuyerId ?? dispute.RaisedBy;
        var buyerStats = await GetPartyStats(buyerId, true, cancellationToken);
        
        // Get seller (from first order item)
        var sellerId = dispute.Order?.OrderItems.FirstOrDefault()?.Product?.SellerId;
        var sellerStats = sellerId.HasValue 
            ? await GetPartyStats(sellerId.Value, false, cancellationToken) 
            : null;

        return new DisputeDetailDto
        {
            Id = dispute.Id,
            CaseId = dispute.CaseId ?? string.Empty,
            Type = dispute.Type,
            Subcategory = dispute.Subcategory,
            Status = dispute.Status,
            Priority = dispute.Priority,
            Amount = dispute.Amount,
            Deadline = dispute.Deadline,
            Description = dispute.Description,
            DesiredOutcome = dispute.DesiredOutcome,
            
            Order = dispute.Order != null ? new OrderInfoDto
            {
                Id = dispute.Order.Id,
                OrderNumber = $"#{dispute.Order.Id}",
                TotalPrice = dispute.Order.TotalPrice,
                PlatformFee = dispute.Order.PlatformFee,
                SellerEarnings = dispute.Order.SellerEarnings,
                OrderDate = dispute.Order.OrderDate,
                CompletedAt = dispute.Order.CompletedAt,
                Status = dispute.Order.Status,
                ProductTitle = dispute.Order.OrderItems.FirstOrDefault()?.Product?.Title,
                ProductImage = dispute.Order.OrderItems.FirstOrDefault()?.Product?.Images,
                ProductPrice = dispute.Order.OrderItems.FirstOrDefault()?.Product?.Price
            } : null,
            
            Buyer = buyerStats,
            Seller = sellerStats,
            
            BuyerEvidences = buyerEvidences,
            SellerEvidences = sellerEvidences,
            OfferHistory = offerHistory,
            NegotiationRounds = dispute.NegotiationRounds,
            LastOfferAmount = dispute.LastOfferAmount,
            
            Messages = dispute.Messages.Select(m => new DisputeMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderUsername = m.Sender?.Username ?? "System",
                SenderType = m.SenderType,
                MessageType = m.MessageType,
                Content = m.Content,
                Attachments = ParseEvidence(m.Attachments),
                OfferAmount = m.OfferAmount,
                OfferReason = m.OfferReason,
                CreatedAt = m.CreatedAt,
                IsRead = m.IsRead,
                IsInternal = m.IsInternal
            }).OrderBy(m => m.CreatedAt).ToList(),
            
            Timeline = timeline,
            CreatedAt = dispute.CreatedAt,
            FirstResponseAt = dispute.FirstResponseAt,
            EscalatedAt = dispute.EscalatedAt,
            
            AssignedTo = dispute.AssignedTo,
            AssignedAt = dispute.AssignedAt,
            ViewCount = dispute.ViewCount,
            LastViewedAt = dispute.LastViewedAt,
            AdminNotes = dispute.AdminNotes,
            
            Winner = dispute.Winner,
            ResolvedBy = dispute.ResolvedBy,
            ResolvedAt = dispute.ResolvedAt,
            RefundAmount = dispute.RefundAmount,
            RefundMethod = dispute.RefundMethod,
            RefundProcessedAt = dispute.RefundProcessedAt,
            
            TrackingNumber = dispute.TrackingNumber,
            DeliveryStatus = dispute.DeliveryStatus,
            RequiresReturn = dispute.RequiresReturn,
            ReturnTrackingNumber = dispute.ReturnTrackingNumber,
            
            IsHighValue = dispute.IsHighValue,
            IsVeRO = dispute.IsVeRO
        };
    }

    private async Task<PartyDto?> GetPartyStats(int? userId, bool isBuyer, CancellationToken cancellationToken)
    {
        if (!userId.HasValue) return null;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        if (user == null) return null;

        var totalOrders = isBuyer
            ? await _context.OrderTables.CountAsync(o => o.BuyerId == userId.Value, cancellationToken)
            : await _context.OrderTables
                .Where(o => o.OrderItems.Any(oi => oi.Product!.SellerId == userId.Value))
                .CountAsync(cancellationToken);

        var totalSpent = isBuyer
            ? await _context.OrderTables
                .Where(o => o.BuyerId == userId.Value)
                .SumAsync(o => o.TotalPrice ?? 0, cancellationToken)
            : 0;

        var disputeCount = await _context.Disputes
            .CountAsync(d => d.RaisedBy == userId.Value, cancellationToken);

        // Get seller rating if applicable
        decimal? averageRating = null;
        int? totalReviews = null;
        
        if (!isBuyer)
        {
            var feedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.SellerId == userId.Value, cancellationToken);
            
            if (feedback != null)
            {
                averageRating = feedback.AverageRating;
                totalReviews = feedback.TotalReviews;
            }
        }

        return new PartyDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            TotalOrders = totalOrders,
            TotalSpent = totalSpent,
            DisputeCount = disputeCount,
            ViolationCount = user.ViolationCount,
            AverageRating = averageRating,
            TotalReviews = totalReviews
        };
    }

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    private List<EvidenceDto> ParseEvidence(string? evidenceJson)
    {
        if (string.IsNullOrEmpty(evidenceJson))
            return new List<EvidenceDto>();

        try
        {
            return JsonSerializer.Deserialize<List<EvidenceDto>>(evidenceJson, _jsonOptions) 
                   ?? new List<EvidenceDto>();
        }
        catch
        {
            return new List<EvidenceDto>();
        }
    }

    private List<OfferDto> ParseOfferHistory(string? offerHistoryJson)
    {
        if (string.IsNullOrEmpty(offerHistoryJson))
            return new List<OfferDto>();

        try
        {
            return JsonSerializer.Deserialize<List<OfferDto>>(offerHistoryJson, _jsonOptions) 
                   ?? new List<OfferDto>();
        }
        catch
        {
            return new List<OfferDto>();
        }
    }

    private List<TimelineEventDto> BuildTimeline(Dispute dispute)
    {
        var timeline = new List<TimelineEventDto>();

        // Created
        timeline.Add(new TimelineEventDto
        {
            Timestamp = dispute.CreatedAt,
            EventType = "Created",
            Actor = dispute.RaisedByNavigation?.Username ?? "Buyer",
            Description = $"Dispute opened: {dispute.Type} - {dispute.Description}",
            Icon = "fa-flag",
            BadgeClass = "badge-danger"
        });

        // First response
        if (dispute.FirstResponseAt.HasValue)
        {
            timeline.Add(new TimelineEventDto
            {
                Timestamp = dispute.FirstResponseAt.Value,
                EventType = "Responded",
                Actor = "Seller",
                Description = "Seller provided response with evidence",
                Icon = "fa-reply",
                BadgeClass = "badge-info"
            });
        }

        // Escalated
        if (dispute.EscalatedAt.HasValue)
        {
            timeline.Add(new TimelineEventDto
            {
                Timestamp = dispute.EscalatedAt.Value,
                EventType = "Escalated",
                Actor = "System",
                Description = "Case escalated to admin for resolution",
                Icon = "fa-arrow-up",
                BadgeClass = "badge-warning"
            });
        }

        // Assigned
        if (dispute.AssignedAt.HasValue)
        {
            timeline.Add(new TimelineEventDto
            {
                Timestamp = dispute.AssignedAt.Value,
                EventType = "Assigned",
                Actor = "Admin",
                Description = "Case assigned to admin for review",
                Icon = "fa-user-check",
                BadgeClass = "badge-primary"
            });
        }

        // Resolved
        if (dispute.ResolvedAt.HasValue)
        {
            var winnerText = dispute.Winner switch
            {
                DisputeWinners.Buyer => "Buyer wins - Full refund",
                DisputeWinners.Seller => "Seller wins - No refund",
                DisputeWinners.Split => $"Split decision - ${dispute.RefundAmount} partial refund",
                _ => "Resolved"
            };

            timeline.Add(new TimelineEventDto
            {
                Timestamp = dispute.ResolvedAt.Value,
                EventType = "Resolved",
                Actor = "Admin",
                Description = $"Case resolved: {winnerText}",
                Icon = "fa-gavel",
                BadgeClass = "badge-success"
            });
        }

        // Refund processed
        if (dispute.RefundProcessedAt.HasValue)
        {
            timeline.Add(new TimelineEventDto
            {
                Timestamp = dispute.RefundProcessedAt.Value,
                EventType = "RefundProcessed",
                Actor = "System",
                Description = $"Refund of ${dispute.RefundAmount} processed successfully",
                Icon = "fa-money-bill",
                BadgeClass = "badge-success"
            });
        }

        return timeline.OrderBy(t => t.Timestamp).ToList();
    }
}
