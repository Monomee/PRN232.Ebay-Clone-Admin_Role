using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

public class AdditionalDataSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdditionalDataSeeder> _logger;

    public int Order => 13; // Run after all other seeders

    public AdditionalDataSeeder(ApplicationDbContext context, ILogger<AdditionalDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting AdditionalDataSeeder...");

        var users = await _context.Users.ToListAsync();
        var products = await _context.Products.ToListAsync();
        var orders = await _context.OrderTables.ToListAsync();
        var disputes = await _context.Disputes.ToListAsync();
        var reviews = await _context.Reviews.ToListAsync();

        if (!users.Any() || !products.Any())
        {
            _logger.LogWarning("Required data (Users or Products) missing for additional seeding.");
            return;
        }

        var random = new Random(123);
        var buyers = users.Where(u => u.Role == "Buyer").ToList();
        var sellers = users.Where(u => u.Role == "Seller").ToList();
        var admins = users.Where(u => u.Role == "Admin" || u.Email != null && u.Email.Contains("admin")).ToList();

        if (!buyers.Any()) buyers = users.Take(5).ToList();
        if (!sellers.Any()) sellers = users.Skip(5).Take(5).ToList();
        if (!admins.Any()) admins = users.Take(1).ToList();

        // 1. Seeds Bids (30 records)
        if (!await _context.Bids.AnyAsync())
        {
            var auctionProducts = products.Where(p => p.IsAuction == true).ToList();
            if (!auctionProducts.Any()) auctionProducts = products.Take(5).ToList();

            var bids = new List<Bid>();
            for (int i = 0; i < 30; i++)
            {
                var product = auctionProducts[random.Next(auctionProducts.Count)];
                var bidder = buyers[random.Next(buyers.Count)];
                bids.Add(new Bid
                {
                    ProductId = product.Id,
                    BidderId = bidder.Id,
                    Amount = (product.Price ?? 100) + (i + 1) * 10,
                    BidTime = DateTime.UtcNow.AddDays(-random.Next(1, 10))
                });
            }
            _context.Bids.AddRange(bids);
            _logger.LogInformation("Seeded 30 Bids.");
        }

        // 2. Seeds Coupons (30 records)
        if (!await _context.Coupons.AnyAsync())
        {
            var coupons = new List<Coupon>();
            for (int i = 0; i < 30; i++)
            {
                coupons.Add(new Coupon
                {
                    Code = $"promo{i:D2}",
                    DiscountPercent = random.Next(5, 50),
                    StartDate = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                    EndDate = DateTime.UtcNow.AddDays(random.Next(1, 30)),
                    MaxUsage = random.Next(10, 500),
                    ProductId = products[random.Next(products.Count)].Id
                });
            }
            _context.Coupons.AddRange(coupons);
            _logger.LogInformation("Seeded 30 Coupons.");
        }

        // 3. Seeds Feedbacks (30 records)
        if (!await _context.Feedbacks.AnyAsync())
        {
            var feedbacks = new List<Feedback>();
            foreach (var seller in sellers.Take(30))
            {
                feedbacks.Add(new Feedback
                {
                    SellerId = seller.Id,
                    AverageRating = (decimal)(3.5 + random.NextDouble() * 1.5),
                    TotalReviews = random.Next(10, 200),
                    PositiveRate = random.Next(80, 100)
                });
            }
            // Ensure we have 30 if sellers count is less
            while(feedbacks.Count < 30)
            {
                var seller = sellers[random.Next(sellers.Count)];
                feedbacks.Add(new Feedback
                {
                    SellerId = seller.Id,
                    AverageRating = (decimal)(3.0 + random.NextDouble() * 2.0),
                    TotalReviews = random.Next(1, 50),
                    PositiveRate = random.Next(60, 100)
                });
            }
            _context.Feedbacks.AddRange(feedbacks.Take(30));
            _logger.LogInformation("Seeded 30 Feedbacks.");
        }

        // 4. Seeds Reviews (30 records)
        if (_context.Reviews.Count() < 10) // Only seed if very few exist
        {
            var reviewList = new List<Review>();
            var comments = new[] { "Great product!", "Fast shipping", "Good quality", "Expected more", "Awesome deal", "Best seller ever", "Poor packaging", "Works perfectly", "High recommend", "Item as described" };
            var reviewStatuses = new[] { "Visible", "Visible", "Visible", "Hidden", "PendingReview" }; // Weighted towards Visible
            for (int i = 0; i < 30; i++)
            {
                reviewList.Add(new Review
                {
                    ProductId = products[random.Next(products.Count)].Id,
                    ReviewerId = buyers[random.Next(buyers.Count)].Id,
                    Rating = random.Next(1, 6),
                    Comment = comments[random.Next(comments.Length)] + " " + i,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60)),
                    Status = reviewStatuses[random.Next(reviewStatuses.Length)]
                });
            }
            _context.Reviews.AddRange(reviewList);
            await _context.SaveChangesAsync(); // Save to get ReviewIds for ReviewReports
            reviews = await _context.Reviews.ToListAsync();
            _logger.LogInformation("Seeded 30 Reviews.");
        }

        // 5. Seeds Messages (30 records)
        if (!await _context.Messages.AnyAsync())
        {
            var messages = new List<Message>();
            var msgContents = new[] { "Is this still available?", "When will you ship?", "Can I get a discount?", "Thanks for the fast delivery", "The item is damaged", "I need a tracking number", "Will you restock this?", "Hi, I have a question about the size" };
            for (int i = 0; i < 30; i++)
            {
                var (sender, receiver) = i % 2 == 0 ? (buyers[random.Next(buyers.Count)], sellers[random.Next(sellers.Count)]) : (sellers[random.Next(sellers.Count)], buyers[random.Next(buyers.Count)]);
                messages.Add(new Message
                {
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    Content = msgContents[random.Next(msgContents.Length)] + " (msg " + i + ")",
                    Timestamp = DateTime.UtcNow.AddMinutes(-i * 120)
                });
            }
            _context.Messages.AddRange(messages);
            _logger.LogInformation("Seeded 30 Messages.");
        }

        // 6. Seeds DisputeMessages (30 records)
        if (!await _context.DisputeMessages.AnyAsync() && disputes.Any())
        {
            var disputeMessages = new List<DisputeMessage>();
            for (int i = 0; i < 30; i++)
            {
                var dispute = disputes[random.Next(disputes.Count)];
                var senderId = random.Next(0, 2) == 0 ? (dispute.RaisedBy ?? buyers[0].Id) : sellers[random.Next(sellers.Count)].Id;
                disputeMessages.Add(new DisputeMessage
                {
                    DisputeId = dispute.Id,
                    SenderId = senderId,
                    SenderType = random.Next(0, 2) == 0 ? "Buyer" : "Seller",
                    Content = "Evidence or rebuttal for dispute " + i,
                    CreatedAt = DateTime.UtcNow.AddHours(-i),
                    MessageType = "Response"
                });
            }
            _context.DisputeMessages.AddRange(disputeMessages);
            _logger.LogInformation("Seeded 30 DisputeMessages.");
        }

        // 7. Seeds ReviewReports (30 records)
        if (!await _context.ReviewReports.AnyAsync() && reviews.Any())
        {
            var reportReasons = new[] { "Spam", "Fake", "Inappropriate", "Harassment", "Other" };
            var reviewReports = new List<ReviewReport>();
            for (int i = 0; i < 30; i++)
            {
                reviewReports.Add(new ReviewReport
                {
                    ReviewId = reviews[random.Next(reviews.Count)].Id,
                    ReporterUserId = users[random.Next(users.Count)].Id,
                    Reason = reportReasons[random.Next(reportReasons.Length)],
                    Description = "Report description " + i,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
                });
            }
            _context.ReviewReports.AddRange(reviewReports);
            _logger.LogInformation("Seeded 30 ReviewReports.");
        }

        // 8. Seeds ProductReports (30 records)
        if (!await _context.ProductReports.AnyAsync())
        {
            var productReports = new List<ProductReport>();
            for (int i = 0; i < 30; i++)
            {
                productReports.Add(new ProductReport
                {
                    ProductId = products[random.Next(products.Count)].Id,
                    ReporterUserId = buyers[random.Next(buyers.Count)].Id,
                    ReporterType = "User",
                    Reason = "Suspicious item " + i,
                    Description = "Detailed description for product report " + i,
                    Status = "Pending",
                    Priority = i % 10 == 0 ? "High" : "Low",
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                });
            }
            _context.ProductReports.AddRange(productReports);
            _logger.LogInformation("Seeded 30 ProductReports.");
        }

        // 9. Seeds ShippingInfos (30 records)
        if (!await _context.ShippingInfos.AnyAsync() && orders.Any())
        {
            var carriers = new[] { "FedEx", "UPS", "DHL", "USPS", "Giao Hang Nhanh" };
            var shippingInfos = new List<ShippingInfo>();
            for (int i = 0; i < 30; i++)
            {
                shippingInfos.Add(new ShippingInfo
                {
                    OrderId = orders[i % orders.Count].Id,
                    Carrier = carriers[random.Next(carriers.Length)],
                    TrackingNumber = "TRACK" + i + random.Next(1000, 9999),
                    Status = "InTransit",
                    EstimatedArrival = DateTime.UtcNow.AddDays(random.Next(1, 7))
                });
            }
            _context.ShippingInfos.AddRange(shippingInfos);
            _logger.LogInformation("Seeded 30 ShippingInfos.");
        }

        // 10. Seeds Payments (30 records)
        if (!await _context.Payments.AnyAsync() && orders.Any())
        {
            var paymentMethods = new[] { "CreditCard", "PayPal", "BankTransfer", "Wallet" };
            var payments = new List<Payment>();
            for (int i = 0; i < 30; i++)
            {
                var order = orders[i % orders.Count];
                payments.Add(new Payment
                {
                    OrderId = order.Id,
                    UserId = order.BuyerId,
                    Amount = order.TotalPrice,
                    Method = paymentMethods[random.Next(paymentMethods.Length)],
                    Status = "Completed",
                    PaidAt = order.OrderDate?.AddMinutes(random.Next(5, 60))
                });
            }
            _context.Payments.AddRange(payments);
            _logger.LogInformation("Seeded 30 Payments.");
        }

        // 11. Seeds AdminActions (30 records)
        if (!await _context.AdminActions.AnyAsync())
        {
            var actions = new[] { "ViewUserProfile", "BanUser", "DeleteProduct", "VerifySeller", "ApproveWithdrawal" };
            var targetTypes = new[] { "User", "Product", "Order", "Review" };
            var adminActions = new List<AdminAction>();
            for (int i = 0; i < 30; i++)
            {
                adminActions.Add(new AdminAction
                {
                    AdminId = admins[random.Next(admins.Count)].Id,
                    Action = actions[random.Next(actions.Length)],
                    TargetType = targetTypes[random.Next(targetTypes.Length)],
                    TargetId = random.Next(1, 100),
                    Details = "{\"reason\":\"Seeded action\",\"batch\":" + i + "}",
                    IpAddress = $"192.168.1.{random.Next(1, 255)}",
                    CreatedAt = DateTime.UtcNow.AddHours(-i)
                });
            }
            _context.AdminActions.AddRange(adminActions);
            _logger.LogInformation("Seeded 30 AdminActions.");
        }

        // 12. Seeds Notifications (30 records)
        if (!await _context.Notifications.AnyAsync())
        {
            var notifications = new List<Notification>();
            for (int i = 0; i < 30; i++)
            {
                notifications.Add(new Notification
                {
                    UserId = buyers[random.Next(buyers.Count)].Id,
                    UserRole = "Buyer",
                    Title = "Notification " + i,
                    Content = "Content for notification " + i,
                    Type = "InApp",
                    Status = "Sent",
                    SentAt = DateTime.UtcNow.AddMinutes(-i * 30),
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedBy = admins[random.Next(admins.Count)].Id
                });
            }
            _context.Notifications.AddRange(notifications);
            _logger.LogInformation("Seeded 30 Notifications.");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("AdditionalDataSeeder completed successfully.");
    }
}
