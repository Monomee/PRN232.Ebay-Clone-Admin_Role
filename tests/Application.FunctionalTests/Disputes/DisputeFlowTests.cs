using EbayClone.Application.Admin.Disputes.Commands.AssignDispute;
using EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.FunctionalTests.Disputes;

using static Testing;

public class DisputeFlowTests : BaseTestFixture
{
    private const string AdminNotes =
        "Admin da xem xet bang chung cua ca hai ben va dua ra quyet dinh cuoi cung cho vu viec nay.";

    [Test]
    public async Task Assign_EscalatedDispute_SetsAssigneeAndStatus()
    {
        var seed = await SeedDisputeAsync();
        RunAsDomainUser(seed.Admin.Id);

        var assigned = await SendAsync(new AssignDisputeCommand(seed.Dispute.Id));

        assigned.ShouldBeTrue();

        var dispute = await FindAsync<Dispute>(seed.Dispute.Id);
        dispute.ShouldNotBeNull();
        dispute.AssignedTo.ShouldBe(seed.Admin.Id);
        dispute.AssignedAt.ShouldNotBeNull();
        dispute.Status.ShouldBe(DisputeStatuses.AssignedToAdmin);

        (await CountAsync<DisputeMessage>()).ShouldBe(1);
        (await CountAsync<AdminAction>()).ShouldBe(1);
    }

    [Test]
    public async Task Resolve_BuyerWins_RefundsOrderAndDeductsDisputedBalance()
    {
        var seed = await SeedDisputeAsync();
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new ResolveDisputeCommand
        {
            DisputeId = seed.Dispute.Id,
            Winner = DisputeWinners.Buyer,
            RefundAmount = 100m,
            AdminNotes = AdminNotes,
            AddSellerViolation = true,
            SendNotifications = false
        });

        var dispute = await FindAsync<Dispute>(seed.Dispute.Id);
        dispute.ShouldNotBeNull();
        dispute.Status.ShouldBe(DisputeStatuses.Resolved);
        dispute.Winner.ShouldBe(DisputeWinners.Buyer);
        dispute.ResolvedBy.ShouldBe(seed.Admin.Id);
        dispute.RefundAmount.ShouldBe(100m);

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Refunded");
        order.SellerEarnings.ShouldBe(0m);

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.DisputedBalance.ShouldBe(0m);
        wallet.TotalRefunded.ShouldBe(100m);

        var seller = await FindAsync<User>(seed.Seller.Id);
        seller.ShouldNotBeNull();
        seller.ViolationCount.ShouldBe(1);

        var refunds = await WhereAsync<FinancialTransaction>(t => t.Type == "Refund");
        refunds.Count.ShouldBe(1);
        refunds[0].Amount.ShouldBe(100m);
        refunds[0].SellerId.ShouldBe(seed.Seller.Id);
    }

    [Test]
    public async Task Resolve_SellerWins_ReleasesFrozenFundsAndRestoresOrder()
    {
        var seed = await SeedDisputeAsync();
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new ResolveDisputeCommand
        {
            DisputeId = seed.Dispute.Id,
            Winner = DisputeWinners.Seller,
            AdminNotes = AdminNotes,
            SendNotifications = false
        });

        var dispute = await FindAsync<Dispute>(seed.Dispute.Id);
        dispute.ShouldNotBeNull();
        dispute.Status.ShouldBe(DisputeStatuses.Resolved);
        dispute.Winner.ShouldBe(DisputeWinners.Seller);
        dispute.RefundAmount.ShouldBe(0m);

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Delivered");

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.PendingBalance.ShouldBe(90m);
        wallet.DisputedBalance.ShouldBe(10m);
        (await CountAsync<FinancialTransaction>()).ShouldBe(0);
    }

    [Test]
    public async Task Resolve_Split_PartiallyRefundsAndReturnsRemainderToSeller()
    {
        var seed = await SeedDisputeAsync();
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new ResolveDisputeCommand
        {
            DisputeId = seed.Dispute.Id,
            Winner = DisputeWinners.Split,
            RefundAmount = 40m,
            AdminNotes = AdminNotes,
            SendNotifications = false
        });

        var dispute = await FindAsync<Dispute>(seed.Dispute.Id);
        dispute.ShouldNotBeNull();
        dispute.Status.ShouldBe(DisputeStatuses.Resolved);
        dispute.Winner.ShouldBe(DisputeWinners.Split);
        dispute.RefundAmount.ShouldBe(40m);

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("PartiallyRefunded");
        order.SellerEarnings.ShouldBe(50m);

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.PendingBalance.ShouldBe(50m);
        wallet.DisputedBalance.ShouldBe(10m);
        wallet.TotalRefunded.ShouldBe(40m);

        var refunds = await WhereAsync<FinancialTransaction>(t => t.Type == "PartialRefund");
        refunds.Count.ShouldBe(1);
        refunds[0].Amount.ShouldBe(40m);
    }

    private static async Task<DisputeSeed> SeedDisputeAsync()
    {
        var admin = await AddUserAsync("dispute-admin", "Administrator");
        var seller = await AddUserAsync("dispute-seller", "Seller");
        var buyer = await AddUserAsync("dispute-buyer", "Buyer");

        var order = new OrderTable
        {
            BuyerId = buyer.Id,
            Status = "Disputed",
            TotalPrice = 100m,
            SellerEarnings = 90m,
            OrderDate = DateTime.UtcNow
        };
        order.OrderItems.Add(new OrderItem
        {
            Quantity = 1,
            UnitPrice = 100m,
            Product = new Product
            {
                Title = "Disputed item",
                SellerId = seller.Id,
                Price = 100m,
                Status = "Active"
            }
        });
        await AddAsync(order);

        var wallet = new SellerWallet
        {
            SellerId = seller.Id,
            DisputedBalance = 100m,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        };
        await AddAsync(wallet);

        var dispute = new Dispute
        {
            CaseId = $"CASE-{Guid.NewGuid():N}"[..12],
            OrderId = order.Id,
            RaisedBy = buyer.Id,
            Status = DisputeStatuses.Escalated,
            Amount = 100m,
            CreatedAt = DateTime.UtcNow
        };
        await AddAsync(dispute);

        return new DisputeSeed(admin, seller, order, wallet, dispute);
    }

    private static async Task<User> AddUserAsync(string username, string role)
    {
        var user = new User
        {
            Username = username,
            Email = $"{username}-{Guid.NewGuid():N}@local",
            Role = role,
            Status = "Active",
            ApprovalStatus = "Approved"
        };
        await AddAsync(user);
        return user;
    }

    private sealed record DisputeSeed(
        User Admin,
        User Seller,
        OrderTable Order,
        SellerWallet Wallet,
        Dispute Dispute);
}
