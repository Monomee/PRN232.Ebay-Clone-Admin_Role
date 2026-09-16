using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.ReturRequests.Queries.GetReturnRequestMessages;

public record ReturnRequestMessageDto
{
    public int Id { get; init; }
    public string? SenderUsername { get; init; }
    public int? SenderId { get; init; }
    public string? Content { get; init; }
    public DateTime? Timestamp { get; init; }
}

public record GetReturnRequestMessagesQuery(int Id) : IRequest<List<ReturnRequestMessageDto>>;

public class GetReturnRequestMessagesQueryHandler : IRequestHandler<GetReturnRequestMessagesQuery, List<ReturnRequestMessageDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReturnRequestMessagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReturnRequestMessageDto>> Handle(GetReturnRequestMessagesQuery request, CancellationToken cancellationToken)
    {
        var returnRequest = await _context.ReturnRequests
            .Include(r => r.Order)
                .ThenInclude(o => o!.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (returnRequest == null) 
            throw new NotFoundException(nameof(ReturnRequest), $"{request.Id}");

        var buyerId = returnRequest.UserId;
        
        // Lấy SellerId từ sản phẩm đầu tiên trong đơn hàng
        var sellerId = returnRequest.Order?.OrderItems.FirstOrDefault()?.Product?.SellerId;

        if (buyerId == null || sellerId == null) 
            return new List<ReturnRequestMessageDto>();

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Where(m => (m.SenderId == buyerId && m.ReceiverId == sellerId) || 
                        (m.SenderId == sellerId && m.ReceiverId == buyerId))
            .OrderBy(m => m.Timestamp)
            .Select(m => new ReturnRequestMessageDto
            {
                Id = m.Id,
                SenderUsername = m.Sender != null ? m.Sender.Username : "Unknown",
                SenderId = m.SenderId,
                Content = m.Content,
                Timestamp = m.Timestamp
            })
            .ToListAsync(cancellationToken);

        return messages;
    }
}
