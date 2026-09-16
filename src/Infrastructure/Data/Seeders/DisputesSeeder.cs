using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

public class DisputesSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DisputesSeeder> _logger;

    public int Order => 9;

    public DisputesSeeder(ApplicationDbContext context, ILogger<DisputesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        int currentDisputeCount = await _context.Disputes.CountAsync();
        if (currentDisputeCount >= 10)
        {
            _logger.LogInformation("Disputes already seeded with 10+ records, skipping...");
            await PatchWalletDisputedBalancesAsync();
            return;
        }

        var johnBuyer = await _context.Users.FirstOrDefaultAsync(u => u.Username == "john_buyer");
        var techSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "tech_seller_pro");
        var fashionSeller = await _context.Users.FirstOrDefaultAsync(u => u.Username == "fashion_boutique");
        var systemUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == "system");

        if (johnBuyer == null || techSeller == null || fashionSeller == null || systemUser == null)
        {
            _logger.LogWarning("Required users not found for dispute seeding");
            return;
        }

        // Wipe out any existing orphaned dummy disputes from old partial seeds if we are regenerating
        if (currentDisputeCount > 0)
        {
            _logger.LogInformation("Dropping old boring disputes to replace with new immersive data...");
            _context.DisputeMessages.RemoveRange(await _context.DisputeMessages.ToListAsync());
            _context.Disputes.RemoveRange(await _context.Disputes.ToListAsync());
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Generating 10 highly immersive E-commerce Dispute scenarios...");

        // 1. CREATE CUSTOM PRODUCTS JUST FOR THESE DISPUTES
        var customProducts = new List<Product>
        {
            new Product { SellerId = techSeller.Id, Title = "Sony G-Master 24-70mm f/2.8 Lens (Pristine)", Price = 1850.00m, Status = "Active", Images = "sony-gm.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = techSeller.Id, Title = "Apple OLED iPad Pro M4 13-inch 512GB", Price = 1499.00m, Status = "Active", Images = "ipad-m4.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = fashionSeller.Id, Title = "Travis Scott x Air Jordan 1 Low 'Reverse Mocha'", Price = 950.00m, Status = "Active", Images = "travis-scott.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = techSeller.Id, Title = "Limited Edition Zelda 1/4 Scale Resin Figure", Price = 420.00m, Status = "Active", Images = "zelda-figure.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = techSeller.Id, Title = "PS5 DualSense Edge Wireless Controller - Brand New", Price = 199.00m, Status = "Active", Images = "dualsense.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = techSeller.Id, Title = "DJI Mini 4 Pro Fly More Combo", Price = 1099.00m, Status = "Active", Images = "dji-mini.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = fashionSeller.Id, Title = "Rolex Submariner Date 116610LN (Box & Papers)", Price = 12500.00m, Status = "Active", Images = "rolex-sub.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = fashionSeller.Id, Title = "Vintage Spider-Man Comic #1 (CGC 8.5)", Price = 850.00m, Status = "Active", Images = "spiderman-cgc.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = techSeller.Id, Title = "Keychron Q1 Pro Custom Mechanical Keyboard (Tactile Brown)", Price = 199.00m, Status = "Active", Images = "keychron.jpg", CreatedAt = DateTime.UtcNow },
            new Product { SellerId = techSeller.Id, Title = "Alienware Aurora R16 Gaming Desktop (RTX 4090)", Price = 3800.00m, Status = "Active", Images = "alienware.jpg", CreatedAt = DateTime.UtcNow }
        };
        _context.Products.AddRange(customProducts);
        await _context.SaveChangesAsync();

        // 2. CREATE IMMERSIVE ORDERS
        var immersiveOrders = new List<OrderTable>
        {
            // O1: Sony Lens - INAD (Escalated Dispute)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-15), TotalPrice = 1850.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-10), CanDisputeUntil = DateTime.UtcNow.AddDays(20), PlatformFee = 185.00m, SellerEarnings = 1665.00m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[0].Id, Quantity = 1, UnitPrice = 1850.00m } } },
            // O2: iPad Pro - Fake Tracking INR (Assigned Dispute)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-22), TotalPrice = 1499.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-18), CanDisputeUntil = DateTime.UtcNow.AddDays(12), PlatformFee = 149.90m, SellerEarnings = 1349.10m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[1].Id, Quantity = 1, UnitPrice = 1499.00m } } },
            // O3: Fake Jordans - Counterfeit (Review Dispute)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-12), TotalPrice = 950.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-8), CanDisputeUntil = DateTime.UtcNow.AddDays(22), PlatformFee = 95.00m, SellerEarnings = 855.00m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[2].Id, Quantity = 1, UnitPrice = 950.00m } } },
            // O4: Crushed Anime Figure - Damaged (Awaiting Seller)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-6), TotalPrice = 420.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-2), CanDisputeUntil = DateTime.UtcNow.AddDays(28), PlatformFee = 42.00m, SellerEarnings = 378.00m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[3].Id, Quantity = 1, UnitPrice = 420.00m } } },
            // O5: Greasy Controller - INAD (Resolved Split Decision, refund 80)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-8), TotalPrice = 199.00m, Status = "PartiallyRefunded", CompletedAt = DateTime.UtcNow.AddDays(-5), CanDisputeUntil = DateTime.UtcNow.AddDays(25), PlatformFee = 19.90m, SellerEarnings = 99.10m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[4].Id, Quantity = 1, UnitPrice = 199.00m } } },
            // O6: Missing Drone Parts - INAD (Open Dispute)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-14), TotalPrice = 1099.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-10), CanDisputeUntil = DateTime.UtcNow.AddDays(20), PlatformFee = 109.90m, SellerEarnings = 989.10m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[5].Id, Quantity = 1, UnitPrice = 1099.00m } } },
            // O7: Fake Rolex - VeRO Auto Takedown (Counterfeit)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-25), TotalPrice = 12500.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-20), CanDisputeUntil = DateTime.UtcNow.AddDays(10), PlatformFee = 1250.00m, SellerEarnings = 11250.00m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[6].Id, Quantity = 1, UnitPrice = 12500.00m } } },
            // O8: Lost Comic - INR (Buyer Wins, full refund)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-45), TotalPrice = 850.00m, Status = "Refunded", CanDisputeUntil = DateTime.UtcNow.AddDays(-15), PlatformFee = 85.00m, SellerEarnings = 0m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[7].Id, Quantity = 1, UnitPrice = 850.00m } } },
            // O9: Wrong Keyboard Switch - INAD (Open)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-4), TotalPrice = 199.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-1), CanDisputeUntil = DateTime.UtcNow.AddDays(29), PlatformFee = 19.90m, SellerEarnings = 179.10m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[8].Id, Quantity = 1, UnitPrice = 199.00m } } },
            // O10: PC DOA - Damaged (Review Dispute)
            new OrderTable { BuyerId = johnBuyer.Id, OrderDate = DateTime.UtcNow.AddDays(-10), TotalPrice = 3800.00m, Status = "InDispute", CompletedAt = DateTime.UtcNow.AddDays(-8), CanDisputeUntil = DateTime.UtcNow.AddDays(22), PlatformFee = 380.00m, SellerEarnings = 3420.00m, OrderItems = new List<OrderItem> { new OrderItem { ProductId = customProducts[9].Id, Quantity = 1, UnitPrice = 3800.00m } } }
        };
        _context.OrderTables.AddRange(immersiveOrders);
        await _context.SaveChangesAsync();

        // 3. CREATE IMMERSIVE DISPUTES
        var immersiveDisputes = new List<Dispute>
        {
            // Case 1: Lens Bait & Switch (Escalated, Toxic argument)
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-LENS", OrderId = immersiveOrders[0].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.INAD, Subcategory = "DamagedItem", Description = "The seller sent me a G-Master lens absolutely coated in fungus with a scratched front element! The listing photos were clearly stolen from someone else. Absolute scam.", DesiredOutcome = "FullRefund", Amount = 1850.00m, Priority = DisputePriorities.Critical, Status = DisputeStatuses.Escalated, CreatedAt = DateTime.UtcNow.AddDays(-4), FirstResponseAt = DateTime.UtcNow.AddDays(-3), EscalatedAt = DateTime.UtcNow.AddDays(-1), Deadline = DateTime.UtcNow.AddHours(23), BuyerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/fungus_lens/400/300\",\"description\":\"Thick fungus inside lens\"},{\"type\":\"video\",\"url\":\"https://www.w3schools.com/html/mov_bbb.mp4\",\"description\":\"Video tilting lens to show deep gouges\"}]", SellerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/lens_pristine/400/300\",\"description\":\"Photo of the lens I ACTUALLY sent\"}]", NegotiationRounds = 3, LastOfferAmount = 900.00m, TrackingNumber = "1Z999SCAM1234", DeliveryStatus = "Delivered", RequiresReturn = true, IsHighValue = true, ViewCount = 2 },
            
            // Case 2: Fake Tracking Zip Code (Admin assigned)
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-IPAD", OrderId = immersiveOrders[1].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.INR, Subcategory = "FakeTracking", Description = "Tracking says 'Delivered to front porch', but the destination zip code on the UPS site is 90210 (Beverly Hills). I live in New York! The seller intentionally shipped an empty envelope to a fake address to game the tracking system.", DesiredOutcome = "FullRefund", Amount = 1499.00m, Priority = DisputePriorities.Critical, Status = DisputeStatuses.AssignedToAdmin, AssignedTo = 1, AssignedAt = DateTime.UtcNow.AddHours(-5), CreatedAt = DateTime.UtcNow.AddDays(-2), EscalatedAt = DateTime.UtcNow.AddHours(-12), Deadline = DateTime.UtcNow.AddHours(5), BuyerEvidence = "[{\"type\":\"document\",\"url\":\"https://www.w3.org/WAI/WCAG21/Techniques/pdf/pdf-sample.pdf\",\"description\":\"UPS official proof of delivery showing wrong city/state\"}]", TrackingNumber = "1ZFAKE990001", DeliveryStatus = "Delivered", RequiresReturn = false, IsHighValue = true, ViewCount = 1 },

            // Case 3: Fake Travis Scott Jordans
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-SNEAK", OrderId = immersiveOrders[2].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.Counterfeit, Subcategory = "FakeBrand", Description = "Ran these through CheckCheck app and they failed instantly. The suede is completely dead, the swoosh placement is 2cm too high, and it smells like toxic factory glue, not Nike leather. Seller is a counterfeiter.", DesiredOutcome = "FullRefund", Amount = 950.00m, Priority = DisputePriorities.High, Status = DisputeStatuses.UnderReview, CreatedAt = DateTime.UtcNow.AddDays(-5), FirstResponseAt = DateTime.UtcNow.AddDays(-4), Deadline = DateTime.UtcNow.AddHours(14), BuyerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/dead_suede/400/300\",\"description\":\"Suede brushing test failed\"},{\"type\":\"document\",\"url\":\"https://www.w3.org/WAI/WCAG21/Techniques/pdf/pdf-sample.pdf\",\"description\":\"Failed Auth Report\"}]", SellerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/snkrs_receipt/400/300\",\"description\":\"SNKRS App Screenshot\"}]", IsVeRO = false, IsHighValue = false, ViewCount = 5 },

            // Case 4: Crushed Zelda Figure
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-CRUSH", OrderId = immersiveOrders[3].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.Damaged, Subcategory = "Shattered", Description = "Who ships a $400 highly fragile resin figure in a plastic bag?! The sword is snapped, the base is shattered, and the collector's box is flattened like a pancake.", DesiredOutcome = "PartialRefund", Amount = 200.00m, Priority = DisputePriorities.Medium, Status = DisputeStatuses.AwaitingSellerResponse, CreatedAt = DateTime.UtcNow.AddHours(-15), Deadline = DateTime.UtcNow.AddHours(57), BuyerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/crushed_box/400/300\",\"description\":\"Poly mailer and crushed box\"},{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/broken_sword/400/300\",\"description\":\"Sword snapped in 3 places\"}]", RequiresReturn = false, ViewCount = 0 },

            // Case 5: Greasy DualSense (Resolved - Split Decision)
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-GREASE", OrderId = immersiveOrders[4].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.INAD, Subcategory = "UsedSoldAsNew", Description = "Listed as 'Brand New Sealed'. Arrived with the seal deliberately cut, thumbsticks are shiny with human grease, and it has massive stick drift to the left.", DesiredOutcome = "FullRefund", Amount = 199.00m, Priority = DisputePriorities.Low, Status = DisputeStatuses.Resolved, Winner = DisputeWinners.Split, ResolvedBy = 1, ResolvedAt = DateTime.UtcNow.AddDays(-1), AdminNotes = "Seller admitted to opening it 'just to hold it'. Granted a partial refund to compensate for used condition.", RefundAmount = 80.00m, RefundMethod = "OriginalPayment", RefundProcessedAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddDays(-4), FirstResponseAt = DateTime.UtcNow.AddDays(-3), EscalatedAt = DateTime.UtcNow.AddDays(-2), ViewCount = 4 },

            // Case 6: Missing Drone Parts
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-DRONE", OrderId = immersiveOrders[5].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.Other, Subcategory = "MissingParts", Description = "I received the drone itself, but the 'Fly More Combo' batteries and the RC Controller are completely missing from the box. Looks like the seller parted it out to sell separately.", DesiredOutcome = "FullRefund", Amount = 1099.00m, Priority = DisputePriorities.High, Status = DisputeStatuses.Open, CreatedAt = DateTime.UtcNow.AddHours(-2), Deadline = DateTime.UtcNow.AddHours(70), BuyerEvidence = "[{\"type\":\"video\",\"url\":\"https://www.w3schools.com/html/mov_bbb.mp4\",\"description\":\"Showing empty compartments after unboxing\"}]", RequiresReturn = true, IsHighValue = true, ViewCount = 0 },

            // Case 7: VeRO Fake Rolex (System Raised)
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-ROLEX", OrderId = immersiveOrders[6].Id, RaisedBy = systemUser.Id, Type = DisputeTypes.Counterfeit, Subcategory = "BrandTakedown", Description = "[SYSTEM AUTO-HOLD] Rolex S.A. Legal Department filed a VeRO takedown regarding this listing immediately after sale. Funds frozen pending intensive physical authentication.", DesiredOutcome = "FullRefund", Amount = 12500.00m, Priority = DisputePriorities.Critical, Status = DisputeStatuses.AssignedToAdmin, AssignedTo = 1, AssignedAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddDays(-2), Deadline = DateTime.UtcNow.AddHours(12), BuyerEvidence = "[{\"type\":\"document\",\"url\":\"https://www.w3.org/WAI/WCAG21/Techniques/pdf/pdf-sample.pdf\",\"description\":\"DMCA/VeRO Legal Document from Rolex S.A.\"}]", SellerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/dealer_stamp/400/300\",\"description\":\"Grey market dealer stamp\"}]", RequiresReturn = false, IsHighValue = true, IsVeRO = true, ViewCount = 28 },

            // Case 8: Lost Comic
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-LOST", OrderId = immersiveOrders[7].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.INR, Subcategory = "LostInTransit", Description = "It's been 45 days. Tracking hasn't updated in a month. I've been patient but I need my money back now.", DesiredOutcome = "FullRefund", Amount = 850.00m, Priority = DisputePriorities.Medium, Status = DisputeStatuses.Resolved, Winner = DisputeWinners.Buyer, ResolvedBy = 1, ResolvedAt = DateTime.UtcNow.AddHours(-10), AdminNotes = "Carrier confirms item is lost in transit. Refunded buyer. Seller instructed to file carrier insurance claim.", RefundAmount = 850.00m, RefundMethod = "StoreCredit", RefundProcessedAt = DateTime.UtcNow.AddHours(-9), CreatedAt = DateTime.UtcNow.AddDays(-6), ViewCount = 3 },

            // Case 9: Wrong Keyboard Switch (Petty argument)
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-KEYB", OrderId = immersiveOrders[8].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.INAD, Subcategory = "WrongColor", Description = "I ordered TACTILE BROWN switches because I work in an office. They sent CLICKY BLUE. My coworkers are literally threatening to murder me because of the typing noise.", DesiredOutcome = "Replacement", Amount = 199.00m, Priority = DisputePriorities.Low, Status = DisputeStatuses.Open, CreatedAt = DateTime.UtcNow.AddHours(-1), Deadline = DateTime.UtcNow.AddHours(71), ViewCount = 0 },

            // Case 10: Dead PC
            new Dispute { CaseId = $"DSP-{DateTime.UtcNow:yyyyMMdd}-DOAPC", OrderId = immersiveOrders[9].Id, RaisedBy = johnBuyer.Id, Type = DisputeTypes.Damaged, Subcategory = "Defective", Description = "Turned it on, fans spin, then immediate sparks and smoke from the GPU slot. The RTX 4090 wasn't shipped with foam expansion packs inside the case, so it tore the PCIe slot completely off the motherboard during shipping.", DesiredOutcome = "FullRefund", Amount = 3800.00m, Priority = DisputePriorities.Critical, Status = DisputeStatuses.UnderReview, CreatedAt = DateTime.UtcNow.AddDays(-2), EscalatedAt = DateTime.UtcNow.AddDays(-1), Deadline = DateTime.UtcNow.AddHours(4), BuyerEvidence = "[{\"type\":\"image\",\"url\":\"https://picsum.photos/seed/torn_pcie/400/300\",\"description\":\"Motherboard PCIe slot destroyed\"},{\"type\":\"video\",\"url\":\"https://www.w3schools.com/html/mov_bbb.mp4\",\"description\":\"Smoke coming out on first boot\"}]", RequiresReturn = true, IsHighValue = true, ViewCount = 7 }
        };
        _context.Disputes.AddRange(immersiveDisputes);
        await _context.SaveChangesAsync();

        // 4. CREATE INTENSE CHAT HISTORIES
        var messages = new List<DisputeMessage>();

        // Chat for Case 1 (Lens Bait & Switch)
        var c1 = immersiveDisputes[0];
        messages.AddRange(new[] {
            new DisputeMessage { DisputeId = c1.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Note, Content = "Is this a joke? The lens is infested with fungus. The front glass looks like it was rubbed with sandpaper.", CreatedAt = c1.CreatedAt.AddHours(1) },
            new DisputeMessage { DisputeId = c1.Id, SenderId = techSeller.Id, SenderType = SenderTypes.Seller, MessageType = MessageTypes.Response, Content = "Nice try, scammer. I took a video of myself packing a flawless lens at the post office. You're trying to send me back your broken trash.", CreatedAt = c1.CreatedAt.AddHours(5) },
            new DisputeMessage { DisputeId = c1.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Evidence, Content = "I HAVE AN UNBOXING VIDEO. Your tape was still sealed. Look at the serial number in my video, it matches yours!", CreatedAt = c1.CreatedAt.AddHours(6) },
            new DisputeMessage { DisputeId = c1.Id, SenderId = techSeller.Id, SenderType = SenderTypes.Seller, MessageType = MessageTypes.Offer, OfferAmount = 900m, OfferReason = "Bribe to go away", Content = "Fine, the post office must have swapped it... I'll give you half your money back if you close this right now. If admin steps in, you get nothing.", CreatedAt = c1.CreatedAt.AddDays(1) },
            new DisputeMessage { DisputeId = c1.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Decline, Content = "Absolutely not. Escalating to admin.", CreatedAt = c1.CreatedAt.AddDays(1).AddHours(2) },
            new DisputeMessage { DisputeId = c1.Id, SenderId = systemUser.Id, SenderType = SenderTypes.System, MessageType = MessageTypes.SystemUpdate, Content = "Case escalated by Buyer. Admin review pending due to conflicting high-value evidence.", CreatedAt = c1.EscalatedAt ?? DateTime.UtcNow }
        });

        // Chat for Case 3 (Fake Jordans)
        var c3 = immersiveDisputes[2];
        messages.AddRange(new[] {
            new DisputeMessage { DisputeId = c3.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Note, Content = "These are terrible replicas. The stitching on the reverse swoosh is crooked.", CreatedAt = c3.CreatedAt.AddHours(2) },
            new DisputeMessage { DisputeId = c3.Id, SenderId = fashionSeller.Id, SenderType = SenderTypes.Seller, MessageType = MessageTypes.Response, Content = "Bro Nike QC is just bad these days. I hit these on SNKRS app directly. Look at my receipt.", CreatedAt = c3.CreatedAt.AddDays(1) },
            new DisputeMessage { DisputeId = c3.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Evidence, Content = "Your receipt doesn't even have your name on it. CheckCheck app authenticated them as FAKE. Return authorized please.", CreatedAt = c3.CreatedAt.AddDays(1).AddHours(5) }
        });

        // Chat for Case 9 (Wrong switch)
        var c9 = immersiveDisputes[8];
        messages.AddRange(new[] {
            new DisputeMessage { DisputeId = c9.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Note, Content = "You sent clicky blue switches. The listing literally said 'Tactile Brown - Silent office use'. My boss yelled at me today.", CreatedAt = c9.CreatedAt.AddMinutes(15) },
            new DisputeMessage { DisputeId = c9.Id, SenderId = techSeller.Id, SenderType = SenderTypes.Seller, MessageType = MessageTypes.Response, Content = "Ah sorry man, I ran out of Browns and figured Blues are gaming switches so you'd prefer them! I can send you a $10 coupon for next time?", CreatedAt = c9.CreatedAt.AddMinutes(45) },
            new DisputeMessage { DisputeId = c9.Id, SenderId = johnBuyer.Id, SenderType = SenderTypes.Buyer, MessageType = MessageTypes.Note, Content = "Are you kidding me? I want to return this immediately.", CreatedAt = c9.CreatedAt.AddMinutes(50) }
        });

        _context.DisputeMessages.AddRange(messages);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Added intense chat histories to disputes.");

        // Unconditionally patch newly seeded disputes so wallet balances match the UI requirements
        await PatchWalletDisputedBalancesAsync();
    }

    private async Task PatchWalletDisputedBalancesAsync()
    {
        var activeDisputes = await _context.Disputes
            .Include(d => d.Order).ThenInclude(o => o!.OrderItems).ThenInclude(oi => oi.Product)
            .Where(d => d.Status != DisputeStatuses.Resolved && d.Status != DisputeStatuses.Closed)
            .ToListAsync();
            
        var sellerWallets = await _context.SellerWallets.ToListAsync();
        
        foreach (var wallet in sellerWallets)
        {
            var sellerDisputes = activeDisputes.Where(d => d.Order?.OrderItems.FirstOrDefault()?.Product?.SellerId == wallet.SellerId);
            var expectedDisputedSum = sellerDisputes.Sum(d => d.Amount ?? d.Order?.TotalPrice ?? 0);
            
            if (wallet.DisputedBalance != expectedDisputedSum)
            {
                var diff = expectedDisputedSum - wallet.DisputedBalance;
                wallet.DisputedBalance = expectedDisputedSum;
                
                if (diff > 0)
                {
                    if (wallet.AvailableBalance >= diff) 
                    {
                        wallet.AvailableBalance -= diff;
                    }
                    else if (wallet.PendingBalance >= diff) 
                    {
                        wallet.PendingBalance -= diff;
                    }
                    else 
                    {
                        var remaining = diff;
                        if (wallet.PendingBalance > 0)
                        {
                            remaining -= wallet.PendingBalance;
                            wallet.PendingBalance = 0;
                        }
                        // To prevent negative balances in demo data, simulate that this money was just earned from the order
                        // This keeps the accounting equation true: TotalEarnings = Pending + Available + Locked + Disputed + Withdrawn + Refunded
                        wallet.CreditPending(remaining);
                        wallet.PendingBalance -= remaining; // Instantly deduct it because it goes to DisputedBalance
                    }
                }
            }
            
            // Rescue any pre-existing negative PendingBalances from legacy seeds
            if (wallet.PendingBalance < 0)
            {
                var absNegative = Math.Abs(wallet.PendingBalance);
                wallet.CreditPending(absNegative);
                // CreditPending naturally brings PendingBalance back to 0 and fixes TotalEarnings
            }
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Auto-Patched SellerWallet DisputedBalances to align with realistic Data.");
    }
}
