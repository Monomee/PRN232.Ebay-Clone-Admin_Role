using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Data.Seeders;

public class AddressesSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AddressesSeeder> _logger;

    public int Order => 8;

    public AddressesSeeder(ApplicationDbContext context, ILogger<AddressesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (_context.Addresses.Any())
        {
            _logger.LogInformation("Addresses already seeded, skipping...");
            return;
        }

        var john = await _context.Users.FirstOrDefaultAsync(u => u.Username == "john_buyer");
        var techSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "tech_seller_pro");
        var fashionSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "fashion_boutique");

        var addresses = new List<Address>();

        // John's addresses
        if (john != null)
        {
            addresses.AddRange(new[]
            {
                new Address
                {
                    UserId = john.Id,
                    FullName = "John Smith",
                    Phone = "+1-555-123-4567",
                    Street = "123 Main Street, Apt 4B",
                    City = "New York",
                    State = "NY",
                    Country = "United States",
                    IsDefault = true
                },
                new Address
                {
                    UserId = john.Id,
                    FullName = "John Smith",
                    Phone = "+1-555-987-6543",
                    Street = "456 Office Building, Suite 200",
                    City = "Brooklyn",
                    State = "NY",
                    Country = "United States",
                    IsDefault = false
                }
            });
        }

        // Tech Seller's business address
        if (techSeller != null)
        {
            addresses.Add(new Address
            {
                UserId = techSeller.Id,
                FullName = "Tech Pro LLC",
                Phone = "+1-555-TECH-PRO",
                Street = "789 Business Park Drive",
                City = "San Francisco",
                State = "CA",
                Country = "United States",
                IsDefault = true
            });
        }

        // Fashion Seller's addresses
        if (fashionSeller != null)
        {
            addresses.AddRange(new[]
            {
                new Address
                {
                    UserId = fashionSeller.Id,
                    FullName = "Fashion Boutique Elite",
                    Phone = "+1-555-FASHION-1",
                    Street = "321 Fashion Avenue, 5th Floor",
                    City = "Los Angeles",
                    State = "CA",
                    Country = "United States",
                    IsDefault = true
                },
                new Address
                {
                    UserId = fashionSeller.Id,
                    FullName = "Fashion Boutique Elite - Warehouse",
                    Phone = "+1-555-FASHION-2",
                    Street = "555 Industrial Blvd, Unit 12",
                    City = "Long Beach",
                    State = "CA",
                    Country = "United States",
                    IsDefault = false
                }
            });
        }

        if (addresses.Any())
        {
            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} Addresses", addresses.Count);
        }
    }
}
