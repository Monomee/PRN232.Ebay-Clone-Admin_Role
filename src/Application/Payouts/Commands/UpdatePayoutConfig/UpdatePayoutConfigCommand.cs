using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Payouts.Commands.UpdatePayoutConfig;

public record UpdatePayoutConfigCommand(
    string Frequency,
    decimal MinimumThreshold,
    int ScheduledHourUtc,
    bool IsEnabled
) : IRequest<int>;

public class UpdatePayoutConfigCommandValidator : AbstractValidator<UpdatePayoutConfigCommand>
{
    public UpdatePayoutConfigCommandValidator()
    {
        RuleFor(v => v.Frequency).Must(f => f is "Daily" or "Weekly").WithMessage("Frequency must be Daily or Weekly.");
        RuleFor(v => v.MinimumThreshold).GreaterThanOrEqualTo(0).WithMessage("Minimum threshold cannot be negative.");
        RuleFor(v => v.ScheduledHourUtc).InclusiveBetween(0, 23).WithMessage("Execution hour must be between 0 and 23 (UTC).");
    }
}

public class UpdatePayoutConfigCommandHandler : IRequestHandler<UpdatePayoutConfigCommand, int>
{
    private readonly IApplicationDbContext _context;

    public UpdatePayoutConfigCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(UpdatePayoutConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _context.PayoutConfigs
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            config = new PayoutConfig();
            _context.PayoutConfigs.Add(config);
        }

        config.Frequency = request.Frequency;
        config.MinimumThreshold = request.MinimumThreshold;
        config.ScheduledHourUtc = request.ScheduledHourUtc;
        config.IsEnabled = request.IsEnabled;
        config.UpdatedAt = DateTime.UtcNow;

        return await _context.SaveChangesAsync(cancellationToken);
    }
}
