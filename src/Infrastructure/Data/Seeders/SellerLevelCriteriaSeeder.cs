using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

public class SellerLevelCriteriaSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger _logger;

    public SellerLevelCriteriaSeeder(ApplicationDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 3; // Early seeding

    public async Task SeedAsync()
    {
        if (await _context.SellerLevelCriteria.AnyAsync())
        {
            return;
        }

        _logger.LogInformation("Seeding default SellerLevelCriteria...");

        var criteria = new SellerLevelCriteria
        {
            TopRatedMinTransactions = 100,
            TopRatedMinSales = 1000m,
            TopRatedMinDays = 90,
            TopRatedMaxUnresolvedCases = 2,
            TopRatedMaxDefectRate = 0.005,
            TopRatedMaxLateRate = 0.03,
            AboveStandardMinDays = 30,
            AboveStandardMaxDefectRate = 0.02,
            AboveStandardMaxUnresolvedCases = 2,
            AboveStandardMaxUnresolvedRate = 0.003,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SellerLevelCriteria.Add(criteria);
        await _context.SaveChangesAsync(default);
    }
}
