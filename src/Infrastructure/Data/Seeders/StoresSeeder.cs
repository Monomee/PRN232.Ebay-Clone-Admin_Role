using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Data.Seeders;

public class StoresSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StoresSeeder> _logger;

    public int Order => 7;

    public StoresSeeder(ApplicationDbContext context, ILogger<StoresSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_context.Stores.Any())
        {
            _logger.LogInformation("Stores already seeded, skipping...");
            return;
        }

        var techSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "tech_seller_pro");
        var fashionSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "fashion_boutique");
        var collectiblesSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "collectibles_expert");

        if (techSeller == null || fashionSeller == null)
        {
            _logger.LogWarning("Required sellers not found, skipping store seeding");
            return;
        }

        var stores = new List<Store>
        {
            new Store
            {
                SellerId = techSeller.Id,
                StoreName = "Tech Pro Electronics",
                Description = "Your trusted source for authentic electronics and gadgets. " +
                             "We offer 100% genuine products with warranty. " +
                             "Fast shipping, excellent customer service. Over 10,000 satisfied customers!",
                BannerImageUrl = "tech-store-banner.jpg"
            },
            new Store
            {
                SellerId = fashionSeller.Id,
                StoreName = "Fashion Boutique Elite",
                Description = "Luxury fashion and accessories. Specializing in designer bags, shoes, and vintage items. " +
                             "All items authenticated and verified. We ship worldwide with insurance.",
                BannerImageUrl = "fashion-store-banner.jpg"
            }
        };

        if (collectiblesSeller != null)
        {
            stores.Add(new Store
            {
                SellerId = collectiblesSeller.Id,
                StoreName = "Rare Collectibles & Memorabilia",
                Description = "Premium sports memorabilia, trading cards, and rare collectibles. " +
                             "Every item comes with certificate of authenticity. " +
                             "Trusted by collectors worldwide since 2015.",
                BannerImageUrl = "collectibles-store-banner.jpg"
            });
        }

        _context.Stores.AddRange(stores);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} Stores", stores.Count);
    }
}
