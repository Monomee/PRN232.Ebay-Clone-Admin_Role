using EbayClone.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Payouts.Queries.GetPayoutConfig;

public record GetPayoutConfigQuery : IRequest<PayoutConfigDto>;

public record PayoutConfigDto(
    int Id,
    string Frequency,
    decimal MinimumThreshold,
    int ScheduledHourUtc,
    bool IsEnabled
);

public class GetPayoutConfigQueryHandler : IRequestHandler<GetPayoutConfigQuery, PayoutConfigDto>
{
    private readonly IApplicationDbContext _context;

    public GetPayoutConfigQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayoutConfigDto> Handle(GetPayoutConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _context.PayoutConfigs
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
            return new PayoutConfigDto(0, "Daily", 10m, 2, true);

        return new PayoutConfigDto(
            config.Id,
            config.Frequency,
            config.MinimumThreshold,
            config.ScheduledHourUtc,
            config.IsEnabled);
    }
}
