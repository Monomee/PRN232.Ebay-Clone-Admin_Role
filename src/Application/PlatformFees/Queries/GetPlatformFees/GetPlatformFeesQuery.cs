using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;

namespace EbayClone.Application.PlatformFees.Queries.GetPlatformFees;

public record PlatformFeesDto
{
    public decimal ListingFee { get; init; }
    public decimal FinalValueFeePercentage { get; init; }
}

public record GetPlatformFeesQuery : IRequest<PlatformFeesDto>;

public class GetPlatformFeesQueryHandler : IRequestHandler<GetPlatformFeesQuery, PlatformFeesDto>
{
    private readonly IApplicationDbContext _context;

    public GetPlatformFeesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformFeesDto> Handle(GetPlatformFeesQuery request, CancellationToken cancellationToken)
    {
        var listingFee = await _context.PlatformFees
            .Where(f => f.FeeType == PlatformFee.TypeListingFee && f.IsActive && f.CategoryId == null)
            .Select(f => f.FixedAmount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var finalValueFee = await _context.PlatformFees
            .Where(f => f.FeeType == PlatformFee.TypeFinalValueFee && f.IsActive && f.CategoryId == null)
            .Select(f => f.Percentage)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        return new PlatformFeesDto
        {
            ListingFee = listingFee,
            FinalValueFeePercentage = finalValueFee
        };
    }
}
