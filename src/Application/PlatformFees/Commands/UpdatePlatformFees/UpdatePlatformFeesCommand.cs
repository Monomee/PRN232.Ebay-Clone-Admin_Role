using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;

namespace EbayClone.Application.PlatformFees.Commands.UpdatePlatformFees;

public record UpdatePlatformFeesCommand : IRequest<int>
{
    public decimal ListingFee { get; init; }
    public decimal FinalValueFeePercentage { get; init; }
}

public class UpdatePlatformFeesCommandHandler : IRequestHandler<UpdatePlatformFeesCommand, int>
{
    private readonly IApplicationDbContext _context;

    public UpdatePlatformFeesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(UpdatePlatformFeesCommand request, CancellationToken cancellationToken)
    {
        // 1. Update Listing Fee
        var listingFee = await _context.PlatformFees
            .FirstOrDefaultAsync(f => f.FeeType == PlatformFee.TypeListingFee && f.IsActive && f.CategoryId == null, cancellationToken);

        if (listingFee == null)
        {
            listingFee = new PlatformFee
            {
                FeeType = PlatformFee.TypeListingFee,
                IsActive = true
            };
            _context.PlatformFees.Add(listingFee);
        }
        listingFee.FixedAmount = request.ListingFee;
        listingFee.EffectiveFrom = DateTime.UtcNow;

        // 2. Update Final Value Fee (Percentage)
        var finalValueFee = await _context.PlatformFees
            .FirstOrDefaultAsync(f => f.FeeType == PlatformFee.TypeFinalValueFee && f.IsActive && f.CategoryId == null, cancellationToken);

        if (finalValueFee == null)
        {
            finalValueFee = new PlatformFee
            {
                FeeType = PlatformFee.TypeFinalValueFee,
                IsActive = true
            };
            _context.PlatformFees.Add(finalValueFee);
        }
        finalValueFee.Percentage = request.FinalValueFeePercentage;
        finalValueFee.EffectiveFrom = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return 1; // Success
    }
}
