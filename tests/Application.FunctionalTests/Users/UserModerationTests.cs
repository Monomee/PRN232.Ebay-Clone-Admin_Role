using EbayClone.Application.Users.Commands.ApproveUser;
using EbayClone.Application.Users.Commands.BanUser;
using EbayClone.Application.Users.Commands.UnbanUser;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.FunctionalTests.Users;

using static Testing;

public class UserModerationTests : BaseTestFixture
{
    [Test]
    public async Task Approve_SetsActiveAndWritesAudit()
    {
        var admin = await AddBuyerAsync("approve-admin", "Active", "Approved");
        RunAsDomainUser(admin.Id, "SuperAdmin");
        var user = await AddBuyerAsync("pending-user", "Pending", "PendingApproval");

        var approved = await SendAsync(new ApproveUserCommand
        {
            UserId = user.Id,
            AdminId = admin.Id
        });

        approved.ShouldBeTrue();

        var saved = await FindAsync<User>(user.Id);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe("Active");
        saved.ApprovalStatus.ShouldBe("Approved");
        saved.ApprovedBy.ShouldBe(admin.Id);
        saved.ApprovedAt.ShouldNotBeNull();

        (await CountAsync<Notification>()).ShouldBe(2);
        (await CountAsync<AdminAction>()).ShouldBe(1);
    }

    [Test]
    public async Task Ban_ThenUnban_RestoresActiveUser()
    {
        var admin = await AddBuyerAsync("ban-admin", "Active", "Approved");
        RunAsDomainUser(admin.Id, "SuperAdmin");
        var user = await AddBuyerAsync("active-user", "Active", "Approved");

        var banned = await SendAsync(new BanUserCommand
        {
            UserId = user.Id,
            AdminId = admin.Id,
            Reason = "Fraud"
        });

        banned.ShouldBeTrue();

        var bannedUser = await FindAsync<User>(user.Id);
        bannedUser.ShouldNotBeNull();
        bannedUser.Status.ShouldBe("Banned");
        bannedUser.BannedReason.ShouldBe("Fraud");
        bannedUser.BannedBy.ShouldBe(admin.Id);
        bannedUser.BannedAt.ShouldNotBeNull();

        var unbanned = await SendAsync(new UnbanUserCommand
        {
            UserId = user.Id,
            AdminId = admin.Id
        });

        unbanned.ShouldBeTrue();

        var restored = await FindAsync<User>(user.Id);
        restored.ShouldNotBeNull();
        restored.Status.ShouldBe("Active");
        restored.BannedReason.ShouldBeNull();
        restored.BannedBy.ShouldBeNull();
        restored.BannedAt.ShouldBeNull();

        (await CountAsync<Notification>()).ShouldBe(4);
        (await CountAsync<AdminAction>()).ShouldBe(2);
    }

    [Test]
    public async Task Ban_WhenUserHasOpenOrder_ThrowsAndLeavesUserActive()
    {
        var admin = await AddBuyerAsync("order-admin", "Active", "Approved");
        RunAsDomainUser(admin.Id, "SuperAdmin");
        var user = await AddBuyerAsync("buyer-with-order", "Active", "Approved");

        await AddAsync(new OrderTable
        {
            BuyerId = user.Id,
            Status = "Processing",
            OrderDate = DateTime.UtcNow,
            TotalPrice = 25m
        });

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            SendAsync(new BanUserCommand
            {
                UserId = user.Id,
                AdminId = admin.Id,
                Reason = "Fraud"
            }));

        exception.Message.ShouldContain("active transactions");

        var saved = await FindAsync<User>(user.Id);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe("Active");
        saved.BannedAt.ShouldBeNull();
        (await CountAsync<AdminAction>()).ShouldBe(0);
    }

    private static async Task<User> AddBuyerAsync(string username, string status, string approvalStatus)
    {
        var user = new User
        {
            Username = username,
            Email = $"{username}@local",
            Role = "Buyer",
            Status = status,
            ApprovalStatus = approvalStatus
        };

        await AddAsync(user);
        return user;
    }
}
