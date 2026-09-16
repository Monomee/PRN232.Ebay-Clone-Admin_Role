using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Data.Seeders;

public class PlatformFeesSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlatformFeesSeeder> _logger;

    public int Order => 3;

    public PlatformFeesSeeder(ApplicationDbContext context, ILogger<PlatformFeesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_context.PlatformFees.Any())
        {
            _logger.LogInformation("Platform Fees already seeded, skipping...");
            return;
        }

        var fees = new List<PlatformFee>
        {
            // Standard Final Value Fee
            new PlatformFee
            {
                FeeType = "FinalValueFee",
                CategoryId = null,
                Percentage = 12.9m,
                FixedAmount = null,
                MinAmount = null,
                MaxAmount = null,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = null
            },
            // Listing Fee
            new PlatformFee
            {
                FeeType = "ListingFee",
                CategoryId = null,
                Percentage = null,
                FixedAmount = 0.35m,
                MinAmount = null,
                MaxAmount = null,
                IsActive = false,
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = null
            },
            // Motors Category
            new PlatformFee
            {
                FeeType = "FinalValueFee",
                CategoryId = 4,
                Percentage = 3.0m,
                FixedAmount = null,
                MinAmount = null,
                MaxAmount = 125.0m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = null
            },
            // Collectibles & Art
            new PlatformFee
            {
                FeeType = "FinalValueFee",
                CategoryId = 5,
                Percentage = 15.0m,
                FixedAmount = null,
                MinAmount = null,
                MaxAmount = null,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = null
            },
            // Promoted Listing Fee
            new PlatformFee
            {
                FeeType = "PromotedListingFee",
                CategoryId = null,
                Percentage = 8.0m,
                FixedAmount = null,
                MinAmount = null,
                MaxAmount = null,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = null
            }
        };

        _context.PlatformFees.AddRange(fees);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} Platform Fees", fees.Count);
    }
}
