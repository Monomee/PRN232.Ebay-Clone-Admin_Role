using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.SystemAdmin.IpBlocks;

public record IpBlockDto(int Id, string Network, string Reason, bool IsAutomatic, int? CreatedBy, DateTime CreatedAt,
    DateTime ExpiresAt, DateTime? LiftedAt, string Status);

/// Newest first; Status: Active | Expired | Lifted.
public record GetIpBlocksQuery : IRequest<List<IpBlockDto>>;

public class GetIpBlocksQueryHandler(IApplicationDbContext db) : IRequestHandler<GetIpBlocksQuery, List<IpBlockDto>>
{
    public async Task<List<IpBlockDto>> Handle(GetIpBlocksQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // ponytail: newest 200 only; add paging when the history gets longer than that
        var rows = await db.IpBlocks.OrderByDescending(b => b.Id).Take(200).ToListAsync(ct);
        return rows.Select(b => new IpBlockDto(b.Id, b.Network, b.Reason, b.IsAutomatic, b.CreatedBy, b.CreatedAt, b.ExpiresAt, b.LiftedAt,
            b.LiftedAt != null ? "Lifted" : b.ExpiresAt <= now ? "Expired" : "Active")).ToList();
    }
}

public record BlockIpCommand(string Network, string Reason, int Days) : IRequest<int>;

public class BlockIpCommandValidator : AbstractValidator<BlockIpCommand>
{
    public BlockIpCommandValidator()
    {
        RuleFor(x => x.Network).Must(n => IpBlocklist.ParseAdminInput(n) != null)
            .WithMessage($"Nhập một IP hoặc dải CIDR hợp lệ (IPv4 rộng nhất /{IpBlocklist.MinIpv4Prefix}, IPv6 rộng nhất /{IpBlocklist.MinIpv6Prefix}).");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200).WithMessage("Vui lòng nhập lý do (tối đa 200 ký tự).");
        RuleFor(x => x.Days).InclusiveBetween(1, 365).WithMessage("Thời hạn từ 1 đến 365 ngày.");
    }
}

public class BlockIpCommandHandler(IApplicationDbContext db, IUser actor, ISensitiveDataLogger log) : IRequestHandler<BlockIpCommand, int>
{
    public async Task<int> Handle(BlockIpCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var block = new IpBlock
        {
            Network = IpBlocklist.ParseAdminInput(request.Network)!.Value.ToString(), Reason = request.Reason.Trim(),
            CreatedBy = actor.GetRequiredActorId(), CreatedAt = now, ExpiresAt = now.AddDays(request.Days),
        };
        db.IpBlocks.Add(block);
        log.Add("IpBlock", "IpBlock", block.Network, "PII", new { block.Reason, block.ExpiresAt });
        await db.SaveChangesAsync(ct);
        return block.Id;
    }
}

public record LiftIpBlockCommand(int Id) : IRequest<bool>;

public class LiftIpBlockCommandHandler(IApplicationDbContext db, IUser actor, ISensitiveDataLogger log) : IRequestHandler<LiftIpBlockCommand, bool>
{
    public async Task<bool> Handle(LiftIpBlockCommand request, CancellationToken ct)
    {
        var block = await db.IpBlocks.FirstOrDefaultAsync(b => b.Id == request.Id, ct);
        if (block == null) return false;
        if (block.LiftedAt != null) return true;
        // Kept, not deleted: the automatic rule reads it and does not re-block on the same evidence.
        block.LiftedAt = DateTime.UtcNow;
        block.LiftedBy = actor.GetRequiredActorId();
        log.Add("IpBlockLift", "IpBlock", block.Network, "PII", new { block.Id, block.Reason, block.IsAutomatic });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
