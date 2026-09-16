using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Data.Seeders;

public class CategoriesSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoriesSeeder> _logger;

    public int Order => 2;

    public CategoriesSeeder(ApplicationDbContext context, ILogger<CategoriesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_context.Categories.Any())
        {
            _logger.LogInformation("Categories already seeded, skipping...");
            return;
        }

        var categories = new List<Category>
        {
            new Category { Name = "Electronics" },
            new Category { Name = "Fashion" },
            new Category { Name = "Home & Garden" },
            new Category { Name = "Motors" },
            new Category { Name = "Collectibles & Art" },
            new Category { Name = "Sports & Outdoors" },
            new Category { Name = "Toys & Hobbies" },
            new Category { Name = "Books, Movies & Music" },
            new Category { Name = "Health & Beauty" },
            new Category { Name = "Business & Industrial" },
            new Category { Name = "Jewelry & Watches" },
            new Category { Name = "Baby Essentials" },
            new Category { Name = "Pet Supplies" },
            new Category { Name = "Musical Instruments" }
        };

        _context.Categories.AddRange(categories);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} Categories", categories.Count);
    }
}
