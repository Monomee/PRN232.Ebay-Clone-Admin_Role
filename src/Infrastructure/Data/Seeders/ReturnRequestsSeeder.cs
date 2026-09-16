using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

public class ReturnRequestsSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReturnRequestsSeeder> _logger;

    public int Order => 10; // After DisputesSeeder (9)

    public ReturnRequestsSeeder(ApplicationDbContext context, ILogger<ReturnRequestsSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var johnBuyer = await _context.Users.FirstOrDefaultAsync(u => u.Username == "john_buyer");
        var fashionSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "fashion_boutique");

        if (johnBuyer == null || fashionSeller == null)
        {
            _logger.LogWarning("Required users not found for return request seeding");
            return;
        }

        int buyerId = johnBuyer.Id;
        int sellerId = fashionSeller.Id;

        // --- PART 1: INITIAL CASES (1-4) ---
        if (!await _context.ReturnRequests.AnyAsync())
        {
            // Create sample orders for return requests
            var rrOrders = new List<OrderTable>
            {
                new OrderTable
                {
                    BuyerId = buyerId,
                    OrderDate = DateTime.UtcNow.AddDays(-20),
                    TotalPrice = 500.00m,
                    Status = "Delivered",
                    CompletedAt = DateTime.UtcNow.AddDays(-15),
                    PlatformFee = 64.50m,
                    SellerEarnings = 435.50m
                },
                new OrderTable
                {
                    BuyerId = buyerId,
                    OrderDate = DateTime.UtcNow.AddDays(-10),
                    TotalPrice = 250.00m,
                    Status = "Delivered",
                    CompletedAt = DateTime.UtcNow.AddDays(-5),
                    PlatformFee = 32.25m,
                    SellerEarnings = 217.75m
                },
                new OrderTable
                {
                    BuyerId = buyerId,
                    OrderDate = DateTime.UtcNow.AddDays(-5),
                    TotalPrice = 120.00m,
                    Status = "Delivered",
                    CompletedAt = DateTime.UtcNow.AddDays(-2),
                    PlatformFee = 15.48m,
                    SellerEarnings = 104.52m
                },
                new OrderTable
                {
                    BuyerId = buyerId,
                    OrderDate = DateTime.UtcNow.AddDays(-2),
                    TotalPrice = 1000.00m,
                    Status = "Delivered",
                    CompletedAt = DateTime.UtcNow.AddDays(-1),
                    PlatformFee = 129.00m,
                    SellerEarnings = 871.00m
                }
            };

            _context.OrderTables.AddRange(rrOrders);
            await _context.SaveChangesAsync();

            var returnRequests = new List<ReturnRequest>
            {
                // Case 1: Pending review
                new ReturnRequest
                {
                    OrderId = rrOrders[0].Id,
                    UserId = buyerId,
                    Reason = "Item defective - screen won't turn on",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    EvidenceImages = "[\"https://picsum.photos/400/300?random=1\", \"https://picsum.photos/400/300?random=2\"]",
                    ShopSolution = "Seller offered 10% partial refund but I want full return."
                },

                // Case 2: Escalated to Admin
                new ReturnRequest
                {
                    OrderId = rrOrders[1].Id,
                    UserId = buyerId,
                    Reason = "Item mismatch - Received Red instead of Blue",
                    Status = "Escalated",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    EvidenceImages = "[\"https://picsum.photos/400/300?random=3\"]",
                    ShopSolution = "Seller claims they sent the right color and refused return."
                },

                // Case 3: Waiting for Local Label
                new ReturnRequest
                {
                    OrderId = rrOrders[2].Id,
                    UserId = buyerId,
                    Reason = "Doesn't fit - ordered XL but fits like M",
                    Status = "WaitingForReturnLabel",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    ResolvedAt = DateTime.UtcNow.AddDays(-1),
                    AdminNote = "Admin adjudicated: Fashion Boutique must provide shipping label as sizing chart was misleading.",
                    ResolutionAction = "RequireReturn",
                    EvidenceImages = "[\"https://picsum.photos/400/300?random=4\"]"
                },

                // Case 4: Already Approved (Closed)
                new ReturnRequest
                {
                    OrderId = rrOrders[3].Id,
                    UserId = buyerId,
                    Reason = "Counterfeit suspected - Logo is printed incorrectly",
                    Status = "Approved",
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    ResolvedAt = DateTime.UtcNow.AddDays(-8),
                    AdminNote = "Counterfeit verified by serial number check. Immediate refund issued via Ebay Fund.",
                    ResolutionAction = "RefundWithoutReturn",
                    IsRefundedByEbayFund = true,
                    EvidenceImages = "[\"https://picsum.photos/400/300?random=5\", \"https://picsum.photos/400/300?random=6\"]"
                }
            };

            _context.ReturnRequests.AddRange(returnRequests);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded Initial {Count} Sample Return Requests", returnRequests.Count);

            // Seed Messages for initial cases
            var initialMessages = new List<Message>
            {
                new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "Hi, the XL shirt I received feels like a Medium. Can I return it?", Timestamp = DateTime.UtcNow.AddDays(-7).AddHours(2) },
                new Message { SenderId = sellerId, ReceiverId = buyerId, Content = "Our sizing is standard. Did you check the chart?", Timestamp = DateTime.UtcNow.AddDays(-7).AddHours(4) },
                new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "Yes, I did. Another XL from your shop fits perfectly, but this one is definitely smaller.", Timestamp = DateTime.UtcNow.AddDays(-7).AddHours(5) },
                new Message { SenderId = sellerId, ReceiverId = buyerId, Content = "We don't accept returns for fit issues unless the tag is wrong. Tag says XL.", Timestamp = DateTime.UtcNow.AddDays(-6) },
                
                new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "This bag is definitely not authentic. The stitching is messy.", Timestamp = DateTime.UtcNow.AddDays(-10).AddHours(1) },
                new Message { SenderId = sellerId, ReceiverId = buyerId, Content = "It is 100% authentic. We buy directly from the factory.", Timestamp = DateTime.UtcNow.AddDays(-10).AddHours(3) },
                new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "The serial number doesn't match the brand's database. I'm opening a return.", Timestamp = DateTime.UtcNow.AddDays(-10).AddHours(5) }
            };
            _context.Messages.AddRange(initialMessages);
            await _context.SaveChangesAsync();
        }

        // --- PART 2: NEW CASE (5) - Damaged Product & Detailed Chat ---
        if (!await _context.ReturnRequests.AnyAsync(r => r.Reason != null && r.Reason.Contains("Camera lens cracked")))
        {
            // Pick some products for the new order
            var products = await _context.Products.Where(p => p.SellerId == sellerId).Take(2).ToListAsync();
            if (products.Count < 2)
            {
                _logger.LogWarning("Not enough products from fashion_seller to create Case 5 order");
            }
            else
            {
                var newOrder = new OrderTable
                {
                    BuyerId = buyerId,
                    OrderDate = DateTime.UtcNow.AddDays(-4),
                    TotalPrice = 1500.00m,
                    Status = "Delivered",
                    CompletedAt = DateTime.UtcNow.AddDays(-1),
                    PlatformFee = 193.50m,
                    SellerEarnings = 1306.50m
                };
                _context.OrderTables.Add(newOrder);
                await _context.SaveChangesAsync();

                _context.OrderItems.AddRange(new List<OrderItem>
                {
                    new OrderItem { OrderId = newOrder.Id, ProductId = products[0]!.Id, Quantity = 1, UnitPrice = 1200.00m },
                    new OrderItem { OrderId = newOrder.Id, ProductId = products[1]!.Id, Quantity = 1, UnitPrice = 300.00m }
                });

                var newRequest = new ReturnRequest
                {
                    OrderId = newOrder.Id,
                    UserId = buyerId,
                    Reason = "Item damaged during shipping - Camera lens cracked",
                    Status = "Escalated",
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    EvidenceImages = "[\"https://picsum.photos/400/300?random=10\", \"https://picsum.photos/400/300?random=11\"]",
                    ShopSolution = "Seller claims the package was handled correctly and refuses refund."
                };
                _context.ReturnRequests.Add(newRequest);

                var newMessages = new List<Message>
                {
                    new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "The camera arrived today but the lens is cracked! The box was also slightly crushed.", Timestamp = DateTime.UtcNow.AddHours(-20) },
                    new Message { SenderId = sellerId, ReceiverId = buyerId, Content = "We packed it with triple-layer bubble wrap. It shouldn't have cracked. Are you sure you didn't drop it?", Timestamp = DateTime.UtcNow.AddHours(-18) },
                    new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "I just opened the box and saw it like this. I have photos of the crushed shipping box too.", Timestamp = DateTime.UtcNow.AddHours(-17) },
                    new Message { SenderId = sellerId, ReceiverId = buyerId, Content = "We don't cover shipping damage once it leaves our warehouse. Contact the courier.", Timestamp = DateTime.UtcNow.AddHours(-15) },
                    new Message { SenderId = buyerId, ReceiverId = sellerId, Content = "That's not how eBay works. You are responsible for safe delivery. I'm escalating this to eBay.", Timestamp = DateTime.UtcNow.AddHours(-13) }
                };
                _context.Messages.AddRange(newMessages);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded Case 5: Damaged Product with chat evidence.");
            }
        }
    }
}
