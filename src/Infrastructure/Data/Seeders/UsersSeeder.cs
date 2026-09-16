using Microsoft.Extensions.Logging;
using EbayClone.Domain.Entities;
using EbayClone.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Infrastructure.Data.Seeders;

public class UsersSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UsersSeeder> _logger;

    public int Order => 4;

    public UsersSeeder(ApplicationDbContext context, ILogger<UsersSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var usersToSeed = new List<User>
        {
            new User
            {
                Username = "system",
                Email = "system@ebay.local",
                Password = BCrypt.Net.BCrypt.HashPassword("SystemAccount123!"),
                Role = "System",
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "john_buyer",
                Email = "john@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = "Buyer",
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "tech_seller_pro",
                Email = "techseller@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = "Seller",
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = true,
                AvatarUrl = null
            },
            new User
            {
                Username = "fashion_boutique",
                Email = "fashion@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = "Seller",
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "pending_seller",
                Email = "pending@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = "Seller",
                Status = "Active",
                ApprovalStatus = "PendingApproval",
                IsVerified = false,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "admin",
                Email = "admin@ebay.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = Roles.Administrator,
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "collectibles_expert",
                Email = "collectibles@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = "Seller",
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "superadmin",
                Email = "superadmin@ebay.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = Roles.SuperAdmin,
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "support",
                Email = "support@ebay.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = Roles.Support,
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            },
            new User
            {
                Username = "monitor",
                Email = "monitor@ebay.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = Roles.Monitor,
                Status = "Active",
                ApprovalStatus = "Approved",
                IsVerified = true,
                ViolationCount = 0,
                TwoFactorEnabled = false,
                AvatarUrl = null
            }
        };

        var addedCount = 0;
        foreach (var user in usersToSeed)
        {
            if (!await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                _context.Users.Add(user);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} additional users", addedCount);
        }
        else
        {
            _logger.LogInformation("All seed users already exist, skipping...");
        }
    }
}
