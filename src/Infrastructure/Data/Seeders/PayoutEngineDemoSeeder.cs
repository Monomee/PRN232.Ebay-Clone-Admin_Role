using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds comprehensive demo data for testing the Automated Payout Engine.
///
/// Scenarios seeded:
///   1. payout_seller_rich   → $500 balance, bank linked → Expected: SUCCESS (~90%)
///   2. payout_seller_poor   → $5 balance, bank linked  → Expected: SKIP (below threshold)
///   3. payout_seller_nobank → $200 balance, NO bank    → Expected: SKIP (no bank)
///   4. payout_seller_susp   → $300 balance, Suspended  → Expected: SKIP (risk check)
///   5. payout_seller_disp   → $150 balance, open dispute → Expected: HOLD
///   6. payout_seller_b      → $80 balance, bank linked  → Expected: SUCCESS or FAILED
///   + PayoutConfig: Frequency=Daily, Min=$10, Enabled=true
///   + Historical PayoutTransactions (success + failed) for the chart
/// </summary>
public class PayoutEngineDemoSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PayoutEngineDemoSeeder> _logger;

    public int Order => 15; // Run last — needs Users, Wallets, Disputes to exist

    public PayoutEngineDemoSeeder(ApplicationDbContext context, ILogger<PayoutEngineDemoSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // ── Guard: skip if already seeded ──────────────────────────────────────
        if (await _context.PayoutConfigs.AnyAsync())
        {
            _logger.LogInformation("PayoutEngineDemoSeeder: Already seeded, skipping.");
            return;
        }

        _logger.LogInformation("PayoutEngineDemoSeeder: Starting...");

        // ── 1. PayoutConfig (singleton) ────────────────────────────────────────
        _context.PayoutConfigs.Add(new PayoutConfig
        {
            Frequency = "Daily",
            MinimumThreshold = 10m,
            ScheduledHourUtc = 2,
            IsEnabled = true,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        _logger.LogInformation("PayoutEngineDemoSeeder: PayoutConfig seeded.");

        // ── 2. Seed Payout Seller accounts ─────────────────────────────────────
        var bankVCB  = @"{""bankName"":""Vietcombank"",""accountNumber"":""0011223344"",""accountName"":""PAYOUT RICH SELLER""}";
        var bankTCB  = @"{""bankName"":""Techcombank"",""accountNumber"":""9988776655"",""accountName"":""PAYOUT SELLER B""}";

        var sellers = new[]
        {
            // Scenario 1: Rich balance + bank → should succeed
            new User
            {
                Username = "payout_seller_rich",
                Email = "payout_rich@test.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = "Seller", Status = "Active", ApprovalStatus = "Approved",
                IsVerified = true, SellerLevel = "TopRated",
                BankAccountMock = bankVCB
            },
            // Scenario 2: Balance below threshold → SKIP
            new User
            {
                Username = "payout_seller_poor",
                Email = "payout_poor@test.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = "Seller", Status = "Active", ApprovalStatus = "Approved",
                IsVerified = true,
                BankAccountMock = bankVCB
            },
            // Scenario 3: No bank linked → SKIP
            new User
            {
                Username = "payout_seller_nobank",
                Email = "payout_nobank@test.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = "Seller", Status = "Active", ApprovalStatus = "Approved",
                IsVerified = true,
                BankAccountMock = null   // deliberately empty
            },
            // Scenario 4: Suspended account → SKIP (risk check)
            new User
            {
                Username = "payout_seller_suspended",
                Email = "payout_suspended@test.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = "Seller", Status = "Suspended", ApprovalStatus = "Approved",
                IsVerified = true,
                BankAccountMock = bankTCB
            },
            // Scenario 5: Dispute hold → HOLD
            new User
            {
                Username = "payout_seller_dispute",
                Email = "payout_dispute@test.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = "Seller", Status = "Active", ApprovalStatus = "Approved",
                IsVerified = true,
                BankAccountMock = bankTCB
            },
            // Scenario 6: Normal balance + bank → SUCCESS or FAILED (random 90/10)
            new User
            {
                Username = "payout_seller_b",
                Email = "payout_b@test.local",
                Password = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                Role = "Seller", Status = "Active", ApprovalStatus = "Approved",
                IsVerified = true,
                BankAccountMock = bankTCB
            },
        };

        foreach (var s in sellers)
        {
            if (!await _context.Users.AnyAsync(u => u.Email == s.Email))
                _context.Users.Add(s);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("PayoutEngineDemoSeeder: {Count} Payout test sellers seeded.", sellers.Length);

        // ── 3. Reload sellers from DB to get their generated IDs ───────────────
        var richSeller  = await _context.Users.FirstAsync(u => u.Email == "payout_rich@test.local");
        var poorSeller  = await _context.Users.FirstAsync(u => u.Email == "payout_poor@test.local");
        var nobankSeller    = await _context.Users.FirstAsync(u => u.Email == "payout_nobank@test.local");
        var suspSeller  = await _context.Users.FirstAsync(u => u.Email == "payout_suspended@test.local");
        var dispSeller  = await _context.Users.FirstAsync(u => u.Email == "payout_dispute@test.local");
        var bSeller     = await _context.Users.FirstAsync(u => u.Email == "payout_b@test.local");

        // ── 4. Seed/Update SellerWallets ───────────────────────────────────────
        await EnsureWallet(richSeller.Id,   availBal: 500m);   // Rich → eligible
        await EnsureWallet(poorSeller.Id,   availBal: 5m);     // Poor → below $10 threshold
        await EnsureWallet(nobankSeller.Id, availBal: 200m);   // NoBank → skip
        await EnsureWallet(suspSeller.Id,   availBal: 300m);   // Suspended → skip
        await EnsureWallet(dispSeller.Id,   availBal: 150m);   // Dispute → hold
        await EnsureWallet(bSeller.Id,      availBal: 80m);    // Normal → success/fail
        await _context.SaveChangesAsync();

        // ── 5. Seed an open Dispute for dispSeller ─────────────────────────────
        // We tie it to RaisedBy so the dispute check query (which looks up orders
        // related to this seller) via a simpler approach: override direct lookup
        // In RunPayoutEngineCommand, the dispute check queries via OrderItems → Product.SellerId.
        // For demo purposes, we seed a raw dispute and test it by checking if the seller
        // has disputes raised AGAINST them (raisedBy is the buyer, orderId links back).
        // Simplest approach: add a dispute where RaisedBy = dispSeller.Id
        // and Status = "Open" with no order (null orderId).
        // We'll also update the payout engine dispute check to handle this more broadly,
        // BUT for now the seeder also seeds a DisputeTransaction with Hold to show in the table.
        var existingDemoCase = await _context.Disputes.FirstOrDefaultAsync(d => d.CaseId == "CASE-DEMO-001");
        
        if (existingDemoCase == null || existingDemoCase.OrderId == null)
        {
            var dummyBuyer = await _context.Users.FirstOrDefaultAsync(u => u.Email == "payout_poor@test.local") ?? poorSeller;

            // Generate a dummy product to establish dispSeller as the seller
            var dummyProduct = new Product
            {
                SellerId = dispSeller.Id,
                Title = "Demo Product for Dispute Hold",
                Price = 150m,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _context.Products.Add(dummyProduct);
            await _context.SaveChangesAsync();

            // Link an order
            var dummyOrder = new OrderTable
            {
                BuyerId = dummyBuyer.Id,
                OrderDate = DateTime.UtcNow.AddDays(-3),
                TotalPrice = 150m,
                Status = "Delivered",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { ProductId = dummyProduct.Id, Quantity = 1, UnitPrice = 150m }
                }
            };
            _context.OrderTables.Add(dummyOrder);
            await _context.SaveChangesAsync();

            if (existingDemoCase == null)
            {
                _context.Disputes.Add(new Dispute
                {
                    OrderId = dummyOrder.Id,
                    RaisedBy = dummyBuyer.Id,   // Valid buyer
                    Description = "Buyer claims item not as described (demo seeded dispute).",
                    Status = "Open",
                    Priority = "High",
                    Amount = 150m,
                    Type = "INAD",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    CaseId = "CASE-DEMO-001"
                });
            }
            else
            {
                existingDemoCase.OrderId = dummyOrder.Id;
                existingDemoCase.RaisedBy = dummyBuyer.Id;
            }
            await _context.SaveChangesAsync();
            
            // Allocate dummy disputed balance to the seller's wallet so resolution flows work
            var wallet = await _context.SellerWallets.FirstOrDefaultAsync(w => w.SellerId == dispSeller.Id);
            if (wallet != null && wallet.DisputedBalance == 0)
            {
                wallet.DisputedBalance = 150m;
                // Leave AvailableBalance intact as the payout engine expects 150m available to trigger the HOLD
                await _context.SaveChangesAsync();
            }
        }

        // ── 6. Historical PayoutTransactions for the chart ─────────────────────
        if (!await _context.PayoutTransactions.AnyAsync())
        {
            var historicalTx = BuildHistoricalTransactions(richSeller.Id, bSeller.Id);
            _context.PayoutTransactions.AddRange(historicalTx);
            await _context.SaveChangesAsync();
            _logger.LogInformation("PayoutEngineDemoSeeder: {Count} historical PayoutTransactions seeded.", historicalTx.Count);
        }

        _logger.LogInformation("PayoutEngineDemoSeeder: Done. Ready to test at /payout-engine → Force Run!");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private async Task EnsureWallet(int sellerId, decimal availBal)
    {
        var wallet = await _context.SellerWallets.FirstOrDefaultAsync(w => w.SellerId == sellerId);
        if (wallet == null)
        {
            _context.SellerWallets.Add(new SellerWallet
            {
                SellerId = sellerId,
                AvailableBalance = availBal,
                PendingBalance = 0,
                LockedBalance = 0,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (wallet.AvailableBalance < availBal)
        {
            wallet.AvailableBalance = availBal;
            wallet.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static List<PayoutTransaction> BuildHistoricalTransactions(int sellerId1, int sellerId2)
    {
        var list = new List<PayoutTransaction>();
        var now = DateTime.UtcNow;

        // Past 7 days of simulated Success records for the chart
        var amounts      = new[] { 120m, 85m, 210m, 55m, 310m, 95m, 175m };
        var amounts2     = new[] { 60m, 0m, 140m, 30m, 0m, 70m, 90m };

        for (int i = 6; i >= 0; i--)
        {
            var day = now.AddDays(-i).Date;
            var session = day.ToString("yyyy-MM-dd") + "T02:00:00.0000000Z";

            if (amounts[6 - i] > 0)
            {
                list.Add(new PayoutTransaction
                {
                    SellerId = sellerId1,
                    Amount = amounts[6 - i],
                    Status = PayoutTransaction.StatusSuccess,
                    BankSnapshot = @"{""bankName"":""Vietcombank"",""accountNumber"":""0011223344"",""accountName"":""PAYOUT RICH SELLER""}",
                    SessionId = session,
                    CreatedAt = day.AddHours(2),
                    CompletedAt = day.AddHours(2).AddSeconds(1)
                });
            }

            if (amounts2[6 - i] > 0)
            {
                list.Add(new PayoutTransaction
                {
                    SellerId = sellerId2,
                    Amount = amounts2[6 - i],
                    Status = PayoutTransaction.StatusSuccess,
                    BankSnapshot = @"{""bankName"":""Techcombank"",""accountNumber"":""9988776655"",""accountName"":""PAYOUT SELLER B""}",
                    SessionId = session,
                    CreatedAt = day.AddHours(2),
                    CompletedAt = day.AddHours(2).AddSeconds(2)
                });
            }
        }

        // Seed 3 exception records (Failed + Hold) for the exceptions table
        list.Add(new PayoutTransaction
        {
            SellerId = sellerId2,
            Amount = 45m,
            Status = PayoutTransaction.StatusFailed,
            ErrorLog = "Bank connection timeout",
            BankSnapshot = @"{""bankName"":""Techcombank"",""accountNumber"":""9988776655"",""accountName"":""PAYOUT SELLER B""}",
            SessionId = now.AddDays(-1).ToString("yyyy-MM-dd") + "T02:00:00.0000000Z",
            CreatedAt = now.AddDays(-1).AddHours(2),
            CompletedAt = now.AddDays(-1).AddHours(2).AddSeconds(3)
        });
        list.Add(new PayoutTransaction
        {
            SellerId = sellerId1,
            Amount = 200m,
            Status = PayoutTransaction.StatusFailed,
            ErrorLog = "Destination account closed",
            BankSnapshot = @"{""bankName"":""Vietcombank"",""accountNumber"":""0011223344"",""accountName"":""PAYOUT RICH SELLER""}",
            SessionId = now.AddDays(-3).ToString("yyyy-MM-dd") + "T02:00:00.0000000Z",
            CreatedAt = now.AddDays(-3).AddHours(2),
            CompletedAt = now.AddDays(-3).AddHours(2).AddSeconds(2)
        });

        return list;
    }
}
