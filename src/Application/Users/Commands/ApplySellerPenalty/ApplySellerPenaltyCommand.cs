using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Users.Commands.ApplySellerPenalty;

public record ApplySellerPenaltyCommand(int UserId, int MarksToDeduct, string Reason, int AdminId = 0) : IRequest<Unit>;

public class ApplySellerPenaltyCommandHandler : IRequestHandler<ApplySellerPenaltyCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;

    public ApplySellerPenaltyCommandHandler(IApplicationDbContext context, IUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ApplySellerPenaltyCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
            throw new NotFoundException(nameof(User), $"{request.UserId}");

        var before = new
        {
            user.PerformanceScore,
            user.ViolationCount,
            user.Status,
            user.BannedReason
        };

        // Deduct performance score
        user.PerformanceScore -= request.MarksToDeduct;
        user.ViolationCount += 1;

        if (user.PerformanceScore <= 0)
        {
            user.PerformanceScore = 0;
            if (user.Status != "Banned")
            {
                user.Status = "Banned";
                user.BannedReason = $"Hiệu suất bán hàng quá thấp do bị trừ điểm: {request.Reason}";
                user.BannedAt = DateTime.UtcNow;
            }
        }

        var after = new
        {
            user.PerformanceScore,
            user.ViolationCount,
            user.Status,
            user.BannedReason
        };

        // Determine acting admin — prefer injected IUser, fallback to command field
        var adminId = int.TryParse(_currentUser.Id, out var parsedId) && parsedId > 0
            ? parsedId
            : request.AdminId;

        _context.AdminActions.Add(new AdminAction
        {
            AdminId = adminId,
            Action = "ApplySellerPenalty",
            TargetType = "User",
            TargetId = user.Id,
            Details = JsonSerializer.Serialize(new
            {
                reason = request.Reason,
                marksDeducted = request.MarksToDeduct,
                before,
                after
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
