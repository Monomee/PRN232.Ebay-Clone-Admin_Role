using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Data.Seeders;

public class ProductsSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductsSeeder> _logger;

    public int Order => 6;

    public ProductsSeeder(ApplicationDbContext context, ILogger<ProductsSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_context.Products.Any())
        {
            _logger.LogInformation("Products already seeded, skipping...");
            return;
        }

        var techSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "tech_seller_pro");
        var fashionSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "fashion_boutique");
        var collectiblesSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "collectibles_expert");

        if (techSeller == null || fashionSeller == null)
        {
            _logger.LogWarning("Required sellers not found, skipping product seeding");
            return;
        }

        var products = new List<Product>
        {
            // Electronics
            new Product
            {
                Title = "Apple iPhone 15 Pro Max - 256GB - Natural Titanium",
                Description = "Brand new sealed iPhone 15 Pro Max. Factory unlocked, works with all carriers. " +
                             "Includes USB-C cable and documentation. 1-year Apple warranty.",
                Price = 1199.99m,
                Images = "[\"iphone15-1.jpg\",\"iphone15-2.jpg\",\"iphone15-3.jpg\"]",
                CategoryId = 1,
                SellerId = techSeller.Id,
                IsAuction = false,
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },
            new Product
            {
                Title = "Sony WH-1000XM5 Wireless Noise Cancelling Headphones - Black",
                Description = "Premium noise cancelling headphones with 30-hour battery life. " +
                             "Industry-leading noise cancellation, crystal clear call quality.",
                Price = 349.99m,
                Images = "[\"sony-headphones.jpg\"]",
                CategoryId = 1,
                SellerId = techSeller.Id,
                IsAuction = false,
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },
            new Product
            {
                Title = "MacBook Pro 16\" M3 Pro - 36GB RAM, 512GB SSD - Space Black",
                Description = "Latest 2024 MacBook Pro with M3 Pro chip. Perfect for professionals. " +
                             "AppleCare+ included until 2027.",
                Price = 2899.00m,
                Images = "[\"macbook-pro.jpg\"]",
                CategoryId = 1,
                SellerId = techSeller.Id,
                IsAuction = false,
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },

            // Fashion
            new Product
            {
                Title = "Vintage Nike Air Jordan 1 Retro High OG - Chicago - Size 10",
                Description = "Authentic 1985 Air Jordan 1 in excellent condition. " +
                             "No box, but comes with authentication certificate. Rare find!",
                Price = 2500.00m,
                Images = "[\"jordan1-chicago.jpg\",\"jordan1-sole.jpg\"]",
                CategoryId = 2,
                SellerId = fashionSeller.Id,
                IsAuction = true,
                AuctionEndTime = DateTime.UtcNow.AddDays(7),
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },
            new Product
            {
                Title = "Gucci Women's Marmont Leather Shoulder Bag - Black",
                Description = "Authentic Gucci Marmont bag with certificate of authenticity. " +
                             "Purchased from Gucci store in Paris. Excellent condition, barely used.",
                Price = 1850.00m,
                Images = "[\"gucci-bag-front.jpg\",\"gucci-bag-side.jpg\"]",
                CategoryId = 2,
                SellerId = fashionSeller.Id,
                IsAuction = false,
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },
            new Product
            {
                Title = "Rolex Submariner Date - Stainless Steel - 41mm - 2023 Model",
                Description = "Brand new unworn Rolex Submariner with full box and papers. " +
                             "Warranty card dated 2023. Investment grade timepiece.",
                Price = 15500.00m,
                Images = "[\"rolex-submariner.jpg\"]",
                CategoryId = 11, // Jewelry & Watches
                SellerId = fashionSeller.Id,
                IsAuction = false,
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },

            // Collectibles & Art
            new Product
            {
                Title = "Pokémon 1st Edition Charizard Holographic Card - PSA 9",
                Description = "Ultra rare 1st Edition Charizard from Base Set. " +
                             "PSA graded 9 (Mint). Serial number verified. Investment piece.",
                Price = 8500.00m,
                Images = "[\"charizard-front.jpg\",\"charizard-back.jpg\",\"psa-cert.jpg\"]",
                CategoryId = 5,
                SellerId = collectiblesSeller?.Id ?? fashionSeller.Id,
                IsAuction = true,
                AuctionEndTime = DateTime.UtcNow.AddDays(10),
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            },
            new Product
            {
                Title = "Michael Jordan Signed Basketball - Upper Deck Authenticated",
                Description = "Official NBA basketball signed by Michael Jordan. " +
                             "Comes with Upper Deck hologram and certificate of authenticity.",
                Price = 3200.00m,
                Images = "[\"mj-signed-ball.jpg\"]",
                CategoryId = 5,
                SellerId = collectiblesSeller?.Id ?? fashionSeller.Id,
                IsAuction = false,
                Status = "Active",
                IsVerified = true,
                ReportCount = 0
            }
        };

        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} Sample Products", products.Count);
    }
}
