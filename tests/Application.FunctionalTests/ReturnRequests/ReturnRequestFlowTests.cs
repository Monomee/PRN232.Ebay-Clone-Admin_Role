using EbayClone.Application.ReturRequests.Commands.ApproveReturnRequest;
using EbayClone.Application.ReturRequests.Commands.RejectReturnRequest;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.FunctionalTests.ReturnRequests;

using static Testing;

public class ReturnRequestFlowTests : BaseTestFixture
{
    [Test]
    public async Task Approve_KeepItem_RefundsOrderFromSellerLockedBalance()
    {
        var seed = await SeedPendingReturnAsync();
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new ApproveReturnRequestCommand(seed.ReturnRequest.Id, "Dong y hoan tien", "KeepItem"));

        var request = await FindAsync<ReturnRequest>(seed.ReturnRequest.Id);
        request.ShouldNotBeNull();
        request.Status.ShouldBe(ReturnStatuses.Approved);
        request.ResolvedByAdminId.ShouldBe(seed.Admin.Id);
        request.ResolvedAt.ShouldNotBeNull();
        request.ResolutionAction.ShouldBe("KeepItem");

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Refunded");

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.LockedBalance.ShouldBe(30m);
        wallet.TotalRefunded.ShouldBe(50m);

        (await CountAsync<PayoutTransaction>()).ShouldBe(1);
        (await CountAsync<AdminAction>()).ShouldBe(1);
    }

    [Test]
    public async Task Reject_PendingRequest_LeavesOrderDelivered()
    {
        var seed = await SeedPendingReturnAsync();
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new RejectReturnRequestCommand(seed.ReturnRequest.Id, "Khong du bang chung"));

        var request = await FindAsync<ReturnRequest>(seed.ReturnRequest.Id);
        request.ShouldNotBeNull();
        request.Status.ShouldBe(ReturnStatuses.Rejected);
        request.AdminNote.ShouldBe("Khong du bang chung");
        request.ResolvedByAdminId.ShouldBe(seed.Admin.Id);

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Delivered");

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.LockedBalance.ShouldBe(80m);
        (await CountAsync<PayoutTransaction>()).ShouldBe(0);
        (await CountAsync<AdminAction>()).ShouldBe(1);
    }

    [Test]
    public async Task Approve_RequireReturn_WaitsForLabelWithoutRefund()
    {
        var seed = await SeedPendingReturnAsync();
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new ApproveReturnRequestCommand(seed.ReturnRequest.Id, "Can gui tra hang", "RequireReturn"));

        var request = await FindAsync<ReturnRequest>(seed.ReturnRequest.Id);
        request.ShouldNotBeNull();
        request.Status.ShouldBe(ReturnStatuses.WaitingForReturnLabel);

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Delivered");

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.LockedBalance.ShouldBe(80m);
        (await CountAsync<PayoutTransaction>()).ShouldBe(0);
    }

    [Test]
    public async Task Approve_EscalatedRequest_StillRefundsOrder()
    {
        var seed = await SeedPendingReturnAsync(ReturnStatuses.Escalated);
        RunAsDomainUser(seed.Admin.Id);

        await SendAsync(new ApproveReturnRequestCommand(seed.ReturnRequest.Id, "Xu ly sau escalate", "KeepItem"));

        var request = await FindAsync<ReturnRequest>(seed.ReturnRequest.Id);
        request.ShouldNotBeNull();
        request.Status.ShouldBe(ReturnStatuses.Approved);

        var order = await FindAsync<OrderTable>(seed.Order.Id);
        order.ShouldNotBeNull();
        order.Status.ShouldBe("Refunded");

        var wallet = await FindAsync<SellerWallet>(seed.Wallet.Id);
        wallet.ShouldNotBeNull();
        wallet.LockedBalance.ShouldBe(30m);
    }

    private static async Task<ReturnSeed> SeedPendingReturnAsync(string status = ReturnStatuses.Pending)
    {
        var admin = await AddUserAsync("return-admin", "Administrator");
        var seller = await AddUserAsync("return-seller", "Seller");
        var buyer = await AddUserAsync("return-buyer", "Buyer");

        var order = new OrderTable
        {
            BuyerId = buyer.Id,
            Status = "Delivered",
            TotalPrice = 60m,
            SellerEarnings = 50m,
            OrderDate = DateTime.UtcNow
        };
        order.OrderItems.Add(new OrderItem
        {
            Quantity = 1,
            UnitPrice = 60m,
            Product = new Product
            {
                Title = "Broken item",
                SellerId = seller.Id,
                Price = 60m,
                Status = "Active"
            }
        });
        await AddAsync(order);

        var wallet = new SellerWallet
        {
            SellerId = seller.Id,
            LockedBalance = 80m,
            UpdatedAt = DateTime.UtcNow
        };
        await AddAsync(wallet);

        var returnRequest = new ReturnRequest
        {
            OrderId = order.Id,
            UserId = buyer.Id,
            Reason = "San pham bi loi",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        await AddAsync(returnRequest);

        return new ReturnSeed(admin, order, wallet, returnRequest);
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

    private sealed record ReturnSeed(User Admin, OrderTable Order, SellerWallet Wallet, ReturnRequest ReturnRequest);
}
