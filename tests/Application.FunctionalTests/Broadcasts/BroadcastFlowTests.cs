using EbayClone.Application.Broadcasts.Commands.SendBroadcast;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.FunctionalTests.Broadcasts;

using static Testing;

public class BroadcastFlowTests : BaseTestFixture
{
    [Test]
    public async Task Send_InAppToAll_SavesSentNotification()
    {
        var admin = await AddAdminAsync();

        var created = await SendAsync(new SendBroadcastCommand
        {
            Title = "Bao tri he thong",
            Content = "He thong bao tri luc 2h sang",
            TargetAudience = "All",
            Channels = ["InApp"],
            CreatedBy = admin.Id
        });

        created.ShouldBe(1);

        var notifications = await WhereAsync<Notification>(n => n.Title == "Bao tri he thong");
        notifications.Count.ShouldBe(1);
        notifications[0].Type.ShouldBe("InApp");
        notifications[0].Status.ShouldBe("Sent");
        notifications[0].UserRole.ShouldBeNull();
        notifications[0].UserId.ShouldBeNull();
        notifications[0].SentAt.ShouldNotBeNull();
        notifications[0].CreatedBy.ShouldBe(admin.Id);

        var actions = await WhereAsync<AdminAction>(a => a.Action == "SendBroadcast");
        actions.Count.ShouldBe(1);
        actions[0].AdminId.ShouldBe(admin.Id);
    }

    [Test]
    public async Task Schedule_SellerBroadcast_SavesBothChannelsWithoutSending()
    {
        var admin = await AddAdminAsync();
        var scheduleAt = DateTime.UtcNow.AddDays(1);

        var created = await SendAsync(new SendBroadcastCommand
        {
            Title = "Khuyen mai seller",
            Content = "Giam gia 50%",
            TargetAudience = "Seller",
            Channels = ["InApp", "Email"],
            ScheduleAt = scheduleAt,
            CreatedBy = admin.Id
        });

        created.ShouldBe(2);

        var notifications = await WhereAsync<Notification>(n => n.Title == "Khuyen mai seller");
        notifications.Count.ShouldBe(2);
        notifications.ShouldAllBe(n =>
            n.Status == "Scheduled" &&
            n.SentAt == null &&
            n.UserRole == "Seller" &&
            n.CreatedBy == admin.Id);

        (await WhereAsync<AdminAction>(a => a.Action == "ScheduleBroadcast")).Count.ShouldBe(1);
    }

    [Test]
    public async Task Send_GroupAudience_StoresGroupRole()
    {
        var admin = await AddAdminAsync();

        await SendAsync(new SendBroadcastCommand
        {
            Title = "Thong bao nhom",
            Content = "Noi dung cho nhom TopRated",
            TargetAudience = "Group",
            TargetGroup = "TopRated",
            Channels = ["InApp"],
            CreatedBy = admin.Id
        });

        var notifications = await WhereAsync<Notification>(n => n.Title == "Thong bao nhom");
        notifications.Count.ShouldBe(1);
        notifications[0].UserRole.ShouldBe("Group:TopRated");
        notifications[0].Status.ShouldBe("Sent");
    }

    private static async Task<User> AddAdminAsync()
    {
        var admin = new User
        {
            Username = "broadcast-admin",
            Email = $"broadcast-admin-{Guid.NewGuid():N}@local",
            Role = "Administrator",
            Status = "Active",
            ApprovalStatus = "Approved"
        };
        await AddAsync(admin);
        return admin;
    }
}
