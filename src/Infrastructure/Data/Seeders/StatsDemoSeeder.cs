using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds high-quality demo data for seller leveling demonstration.
/// Creates 7 named sellers (Alice, Bob, Carol, David, Eva, Frank, Grace) 
/// each with a store, products, and properly linked completed orders with OrderItems.
/// Buyers: 13 named buyers who place orders.
/// 
/// Seller profile goals:
///   Alice   → TopRated      (35 orders, first ~175 days ago)
///   Bob     → TopRated      (28 orders, first ~145 days ago)
///   Carol   → AboveStandard (12 orders, first ~55 days ago)
///   David   → AboveStandard (8 orders,  first ~38 days ago)
///   Eva     → BelowStandard (5 orders,  first ~22 days ago – under 30-day threshold)
///   Frank   → BelowStandard (0 orders,  joined 10 days ago)
///   Grace   → BelowStandard (0 orders,  joined 3 days ago)
/// </summary>
public class StatsDemoSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StatsDemoSeeder> _logger;

    public int Order => 12; // Run after FinanceDemoSeeder

    public StatsDemoSeeder(ApplicationDbContext context, ILogger<StatsDemoSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        const string Sentinel = "StatsDemoSeeder-v2";

        if (await _context.FinancialTransactions.AnyAsync(t => t.Description == Sentinel))
        {
            _logger.LogInformation("StatsDemoSeeder-v2 already ran, skipping.");
            return;
        }

        _logger.LogInformation("Starting StatsDemoSeeder-v2 (quality data)...");

        var now = DateTime.UtcNow;
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!");

        // ─────────────────────────────────────────────────────────────────
        // 1. SELLERS
        // ─────────────────────────────────────────────────────────────────
        var sellerDefs = new[]
        {
            new { Username = "alice_shop",   Email = "alice@demo.local",  Level = "TopRated",       DaysAgo = 185, FirstOrderDaysAgo = 175, OrderCount = 35 },
            new { Username = "bob_market",   Email = "bob@demo.local",    Level = "TopRated",       DaysAgo = 155, FirstOrderDaysAgo = 145, OrderCount = 28 },
            new { Username = "carol_deals",  Email = "carol@demo.local",  Level = "AboveStandard",  DaysAgo = 95,  FirstOrderDaysAgo = 55,  OrderCount = 12 },
            new { Username = "david_store",  Email = "david@demo.local",  Level = "AboveStandard",  DaysAgo = 65,  FirstOrderDaysAgo = 38,  OrderCount = 8  },
            new { Username = "eva_new",      Email = "eva@demo.local",    Level = "BelowStandard",  DaysAgo = 30,  FirstOrderDaysAgo = 22,  OrderCount = 5  },
            new { Username = "frank_fresh",  Email = "frank@demo.local",  Level = "BelowStandard",  DaysAgo = 10,  FirstOrderDaysAgo = 0,   OrderCount = 0  },
            new { Username = "grace_rookie", Email = "grace@demo.local",  Level = "BelowStandard",  DaysAgo = 3,   FirstOrderDaysAgo = 0,   OrderCount = 0  },
        };

        var sellers = new List<User>();
        foreach (var def in sellerDefs)
        {
            // Skip if already exists
            if (await _context.Users.AnyAsync(u => u.Email == def.Email)) continue;

            sellers.Add(new User
            {
                Username         = def.Username,
                Email            = def.Email,
                Password         = passwordHash,
                Role             = "Seller",
                Status           = "Active",
                ApprovalStatus   = "Approved",
                ApprovedAt       = now.AddDays(-def.DaysAgo),
                IsVerified       = true,
                TwoFactorEnabled = false,
                SellerLevel      = def.Level
            });
        }

        if (sellers.Any())
        {
            _context.Users.AddRange(sellers);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} demo sellers.", sellers.Count);
        }

        // Reload all sellers including pre-existing ones
        var allSellers = new List<User>();
        foreach (var def in sellerDefs)
        {
            var s = await _context.Users.FirstOrDefaultAsync(u => u.Email == def.Email);
            if (s != null) allSellers.Add(s);
        }

        // ─────────────────────────────────────────────────────────────────
        // 2. BUYERS (13 named buyers)
        // ─────────────────────────────────────────────────────────────────
        var buyerNames = new[]
        {
            ("buyer_henry",   "henry@demo.local"),
            ("buyer_iris",    "iris@demo.local"),
            ("buyer_jack",    "jack@demo.local"),
            ("buyer_kate",    "kate@demo.local"),
            ("buyer_leo",     "leo@demo.local"),
            ("buyer_mia",     "mia@demo.local"),
            ("buyer_nate",    "nate@demo.local"),
            ("buyer_olivia",  "olivia@demo.local"),
            ("buyer_peter",   "peter@demo.local"),
            ("buyer_quinn",   "quinn@demo.local"),
            ("buyer_rose",    "rose@demo.local"),
            ("buyer_sam",     "sam@demo.local"),
            ("buyer_tina",    "tina@demo.local"),
        };

        var buyers = new List<User>();
        foreach (var (username, email) in buyerNames)
        {
            if (await _context.Users.AnyAsync(u => u.Email == email)) continue;

            buyers.Add(new User
            {
                Username         = username,
                Email            = email,
                Password         = passwordHash,
                Role             = "Buyer",
                Status           = "Active",
                ApprovalStatus   = "Approved",
                ApprovedAt       = now.AddDays(-new Random().Next(30, 200)),
                IsVerified       = true,
                TwoFactorEnabled = false
            });
        }

        if (buyers.Any())
        {
            _context.Users.AddRange(buyers);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} demo buyers.", buyers.Count);
        }

        // Reload all buyers
        var allBuyers = new List<User>();
        foreach (var (_, email) in buyerNames)
        {
            var b = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (b != null) allBuyers.Add(b);
        }

        if (!allBuyers.Any())
        {
            _logger.LogWarning("No buyers found – cannot create orders.");
            return;
        }

        // ─────────────────────────────────────────────────────────────────
        // 3. STORES (one per seller)
        // ─────────────────────────────────────────────────────────────────
        var storeNames = new Dictionary<string, string>
        {
            ["alice@demo.local"]  = "Alice's Electronics Hub",
            ["bob@demo.local"]    = "Bob's Gadget Market",
            ["carol@demo.local"]  = "Carol's Fashion Deals",
            ["david@demo.local"]  = "David's Home & Kitchen",
            ["eva@demo.local"]    = "Eva's Lifestyle Shop",
            ["frank@demo.local"]  = "Frank's Fresh Start",
            ["grace@demo.local"]  = "Grace's New Corner",
        };

        foreach (var seller in allSellers)
        {
            if (seller.Email == null) continue;
            if (await _context.Stores.AnyAsync(s => s.SellerId == seller.Id)) continue;

            _context.Stores.Add(new Store
            {
                SellerId    = seller.Id,
                StoreName   = storeNames.TryGetValue(seller.Email, out var name) ? name : seller.Username + "'s Store",
                Description = $"Official store of {seller.Username}. Quality products, fast shipping."
            });
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded stores for demo sellers.");

        // ─────────────────────────────────────────────────────────────────
        // 4. PRODUCTS (2–3 per seller)
        // ─────────────────────────────────────────────────────────────────
        var sellerProducts = new Dictionary<string, (string Title, decimal Price)[]>
        {
            ["alice@demo.local"] = new[]
            {
                ("Wireless Noise-Cancelling Headphones", 149.99m),
                ("4K USB-C Monitor 27\"", 389.00m),
                ("Mechanical Gaming Keyboard", 89.99m),
            },
            ["bob@demo.local"] = new[]
            {
                ("Smartphone Fast Charger 65W", 34.99m),
                ("Portable Bluetooth Speaker", 59.00m),
                ("Smart Watch Fitness Band", 119.99m),
            },
            ["carol@demo.local"] = new[]
            {
                ("Classic Leather Handbag", 75.00m),
                ("Summer Linen Dress Set", 49.99m),
            },
            ["david@demo.local"] = new[]
            {
                ("Non-Stick Cookware Set 5-Piece", 65.00m),
                ("Bamboo Cutting Board Pack", 28.00m),
            },
            ["eva@demo.local"] = new[]
            {
                ("Scented Candle Gift Box", 24.99m),
                ("Yoga Mat Premium", 39.99m),
            },
            ["frank@demo.local"] = new[]
            {
                ("Reusable Water Bottle 1L", 19.99m),
            },
            ["grace@demo.local"] = new[]
            {
                ("Plant Pot Ceramic Set", 22.50m),
            },
        };

        // Track products per seller for order creation
        var productMap = new Dictionary<int, List<Product>>(); // sellerId → products

        foreach (var seller in allSellers)
        {
            if (seller.Email == null) continue;
            if (!sellerProducts.TryGetValue(seller.Email, out var prods)) continue;

            productMap[seller.Id] = new List<Product>();

            foreach (var (title, price) in prods)
            {
                if (await _context.Products.AnyAsync(p => p.SellerId == seller.Id && p.Title == title)) 
                {
                    // Product exists – load it
                    var existing = await _context.Products.FirstOrDefaultAsync(p => p.SellerId == seller.Id && p.Title == title);
                    if (existing != null) productMap[seller.Id].Add(existing);
                    continue;
                }

                var product = new Product
                {
                    Title      = title,
                    Description = $"High quality {title.ToLower()} from {seller.Username}.",
                    Price      = price,
                    SellerId   = seller.Id,
                    Status     = "Active",
                    IsVerified = true,
                    IsAuction  = false
                };
                _context.Products.Add(product);
                await _context.SaveChangesAsync(); // save immediately to get Id
                productMap[seller.Id].Add(product);
            }
        }
        _logger.LogInformation("Seeded products for demo sellers.");

        // ─────────────────────────────────────────────────────────────────
        // 5. ORDERS WITH ORDER ITEMS
        // ─────────────────────────────────────────────────────────────────
        var rng = new Random(99);
        var orderDefs = new[]
        {
            new { Email = "alice@demo.local",  FirstOrderDaysAgo = 175, TotalOrders = 35 },
            new { Email = "bob@demo.local",    FirstOrderDaysAgo = 145, TotalOrders = 28 },
            new { Email = "carol@demo.local",  FirstOrderDaysAgo = 55,  TotalOrders = 12 },
            new { Email = "david@demo.local",  FirstOrderDaysAgo = 38,  TotalOrders = 8  },
            new { Email = "eva@demo.local",    FirstOrderDaysAgo = 22,  TotalOrders = 5  },
        };

        foreach (var def in orderDefs)
        {
            var seller = allSellers.FirstOrDefault(s => s.Email == def.Email);
            if (seller == null) continue;
            if (!productMap.TryGetValue(seller.Id, out var products) || !products.Any()) continue;

            // Check if this seller already has orders
            var hasOrders = await _context.OrderTables
                .AnyAsync(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == seller.Id));
            if (hasOrders) continue;

            // Spread orders evenly from firstOrderDaysAgo to today
            var spread = def.FirstOrderDaysAgo;
            for (int i = 0; i < def.TotalOrders; i++)
            {
                // Orders spread evenly from `spread` days ago down to 1 day ago
                var daysAgo = spread - (int)((double)i / def.TotalOrders * (spread - 1));
                var orderDate = now.AddDays(-daysAgo).AddHours(rng.Next(8, 20));
                var product   = products[rng.Next(products.Count)];
                var qty       = rng.Next(1, 4);
                var unitPrice = product.Price ?? 50m;
                var total     = unitPrice * qty;
                var fee       = Math.Round(total * 0.05m, 2);

                var order = new OrderTable
                {
                    BuyerId          = allBuyers[rng.Next(allBuyers.Count)].Id,
                    OrderDate        = orderDate,
                    Status           = "Completed",
                    TotalPrice       = total,
                    PlatformFee      = fee,
                    SellerEarnings   = total - fee,
                    CompletedAt      = orderDate.AddDays(rng.Next(2, 7)),
                    CanDisputeUntil  = orderDate.AddDays(14),
                    OrderItems = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            ProductId = product.Id,
                            Quantity  = qty,
                            UnitPrice = unitPrice
                        }
                    }
                };

                _context.OrderTables.Add(order);
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} orders for {Email}.", def.TotalOrders, def.Email);
        }

        // ─────────────────────────────────────────────────────────────────
        // 6. SELLER WALLETS
        // ─────────────────────────────────────────────────────────────────
        var walletData = new Dictionary<string, decimal>
        {
            ["alice@demo.local"]  = 3_850.00m,
            ["bob@demo.local"]    = 2_210.50m,
            ["carol@demo.local"]  = 890.25m,
            ["david@demo.local"]  = 545.00m,
            ["eva@demo.local"]    = 118.75m,
            ["frank@demo.local"]  = 0m,
            ["grace@demo.local"]  = 0m,
        };

        foreach (var seller in allSellers)
        {
            if (seller.Email == null) continue;
            if (await _context.SellerWallets.AnyAsync(w => w.SellerId == seller.Id)) continue;

            var avail = walletData.TryGetValue(seller.Email, out var a) ? a : 0m;

            var wallet = new SellerWallet { SellerId = seller.Id };
            if (avail > 0) wallet.CreditAvailable(avail);

            _context.SellerWallets.Add(wallet);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded wallets for demo sellers.");

        // ─────────────────────────────────────────────────────────────────
        // 7. REVENUE – 90 days of FeeDeduction transactions for Revenue Trend chart
        // ─────────────────────────────────────────────────────────────────
        _logger.LogInformation("Seeding demo revenue transactions (90 days)...");

        var revenueRng = new Random(77);
        var systemUser = await _context.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
        var systemUserId = systemUser?.Id ?? 1;

        var revenueTransactions = new List<FinancialTransaction>();
        for (int day = 90; day >= 1; day--)
        {
            var date = now.Date.AddDays(-day);
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            // Trend: grows from ~2M on day 90 to ~12M on day 1
            double trend   = 2_000_000 + (90 - day) * 115_000.0;
            double noise   = (revenueRng.NextDouble() - 0.45) * 800_000;
            double weekMod = isWeekend ? 0.55 : 1.0;
            var amount = (decimal)Math.Max(200_000, (trend + noise) * weekMod);

            // Split into 3–8 sub-transactions per day for realism
            int txCount = revenueRng.Next(3, 9);
            var remaining = amount;
            for (int t = 0; t < txCount; t++)
            {
                decimal slice = t == txCount - 1
                    ? remaining
                    : (decimal)(revenueRng.NextDouble() * (double)remaining * 0.5);
                remaining -= slice;
                if (slice < 1000) slice = 1000;

                revenueTransactions.Add(new FinancialTransaction
                {
                    Type        = "FeeDeduction",
                    Amount      = slice,
                    BalanceAfter = 0,
                    UserId      = systemUserId,
                    SellerId    = systemUserId,
                    Date        = date.AddHours(revenueRng.Next(8, 22)).AddMinutes(revenueRng.Next(0, 60)),
                    Description = "StatsDemoSeeder-v2-revenue"
                });
            }
        }
        _context.FinancialTransactions.AddRange(revenueTransactions);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} revenue transactions.", revenueTransactions.Count);

        // ─────────────────────────────────────────────────────────────────
        // 8. SENTINEL (idempotency marker)
        // ─────────────────────────────────────────────────────────────────
        _context.FinancialTransactions.Add(new FinancialTransaction
        {
            Type         = "FeeDeduction",
            Amount       = 0,
            BalanceAfter = 0,
            UserId       = systemUserId,
            SellerId     = systemUserId,
            Date         = now,
            Description  = Sentinel
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("StatsDemoSeeder-v2 done: 7 sellers, 13 buyers, stores, products, orders, and {Count} revenue transactions seeded.", revenueTransactions.Count);
    }
}
