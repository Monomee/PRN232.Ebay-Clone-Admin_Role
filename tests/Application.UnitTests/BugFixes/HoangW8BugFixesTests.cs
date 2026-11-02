using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Admin.Disputes.Commands.AssignDispute;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeDocket;
using EbayClone.Application.AdminRoles.Commands.CreateAdminRole;
using EbayClone.Application.AdminRoles.Commands.CreateAdminUser;
using EbayClone.Application.AdminRoles.Queries.GetAdminUsers;
using EbayClone.Application.AuditLogs.Queries.GetAuditLogs;
using EbayClone.Application.Authentication.Commands.Login;
using EbayClone.Application.Broadcasts.Commands.SendBroadcast;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Dashboard.Queries.GetDashboardMetrics;
using EbayClone.Application.Financials.Queries.GetSellerPendingFunds;
using EbayClone.Application.Orders.Queries.GetOrdersWithPagination;
using EbayClone.Application.Payouts.Commands.RunPayoutEngine;
using EbayClone.Application.Payouts.Commands.UpdatePayoutConfig;
using EbayClone.Application.PlatformFees.Commands.UpdatePlatformFees;
using EbayClone.Application.Stats.Queries;
using EbayClone.Application.UnitTests.Security;
using EbayClone.Application.Users.Commands.RejectUser;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.BugFixes;

/// Regression tests for the W8 [BUG] tasks assigned to Hoàng (ClickUp ids in each test name's comment).
[TestFixture]
public class HoangW8BugFixesTests
{
    private ApplicationDbContext _db = null!;
    private static readonly IUser Admin = SensitiveLogTests.Actor("42", Roles.SuperAdmin);

    [SetUp] public void SetUp() => _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [TearDown] public void TearDown() => _db.Dispose();

    private async Task<User> AddUser(string name, string role = "Buyer", string? password = null)
    {
        var u = new User { Username = name, Email = $"{name}@test.local", Role = role, Status = "Active",
            Password = password ?? BCrypt.Net.BCrypt.HashPassword("Passw0rd!") };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    // z8rp3fcgjh
    [Test]
    public async Task CreateAdminRole_EmptyOrDuplicateName_IsInvalid()
    {
        _db.AdminRoles.Add(new AdminRole { RoleName = "SuperAdmin" });
        await _db.SaveChangesAsync();
        var validator = new CreateAdminRoleCommandValidator(_db);

        (await validator.ValidateAsync(new CreateAdminRoleCommand { RoleName = "  " })).IsValid.ShouldBeFalse();
        (await validator.ValidateAsync(new CreateAdminRoleCommand { RoleName = "superadmin " })).IsValid.ShouldBeFalse();
        (await validator.ValidateAsync(new CreateAdminRoleCommand { RoleName = "QA Lead" })).IsValid.ShouldBeTrue();
    }

    // z8rp3fcg9n
    [Test]
    public async Task CreateAdminUser_InvalidInput_IsInvalid_AndCreatedAdminCanLogIn()
    {
        var validator = new CreateAdminUserCommandValidator();
        validator.Validate(new CreateAdminUserCommand { Username = "", Email = "not-an-email", Password = "123", RoleId = 1 })
            .Errors.Select(e => e.PropertyName).ShouldBe(["Username", "Email", "Password"], ignoreOrder: true);

        var role = new AdminRole { RoleName = "QA Lead", Permissions = "[\"ManageOrders\"]" };
        _db.AdminRoles.Add(role);
        await _db.SaveChangesAsync();

        var id = await new CreateAdminUserCommandHandler(_db, Mock.Of<ISensitiveDataLogger>(), Admin).Handle(
            new CreateAdminUserCommand { Username = "qa", Email = "qa@test.local", Password = "Passw0rd!", RoleId = role.Id },
            CancellationToken.None);

        var user = await _db.Users.SingleAsync(u => u.Id == id);
        user.Role.ShouldBe("QA Lead");
        user.Password.ShouldNotBe("Passw0rd!");

        var login = await new LoginCommandHandler(_db, Mock.Of<IJwtService>()).Handle(new LoginCommand("qa@test.local", "Passw0rd!"), CancellationToken.None);
        login.Success.ShouldBeTrue();
    }

    // z8rp3fcg9n: accounts created before hashing hold plaintext; login must fail cleanly, not 500
    [Test]
    public async Task Login_LegacyPlaintextPassword_ReturnsFailureInsteadOfThrowing()
    {
        await AddUser("legacy", Roles.Administrator, password: "plain");
        var login = await new LoginCommandHandler(_db, Mock.Of<IJwtService>()).Handle(new LoginCommand("legacy@test.local", "plain"), CancellationToken.None);
        login.Success.ShouldBeFalse();
    }

    // z8rp3fcgjj
    [Test]
    public async Task CustomRole_StaysInAdminList_AndItsPermissionsAreIssued()
    {
        var qa = await AddUser("qa", "QA Lead");
        var role = new AdminRole { RoleName = "QA Lead", Permissions = "[\"ManageOrders\",\"ViewDashboard\"]" };
        _db.AdminRoles.Add(role);
        await _db.SaveChangesAsync();
        _db.AdminUserRoles.Add(new AdminUserRole { UserId = qa.Id, RoleId = role.Id });
        await _db.SaveChangesAsync();

        var list = await new GetAdminUsersQueryHandler(_db).Handle(new GetAdminUsersQuery(), CancellationToken.None);
        list.Items.ShouldContain(x => x.UserId == qa.Id && x.RoleName == "QA Lead");

        (await _db.GetAdminPermissionsAsync(qa.Id, CancellationToken.None)).ShouldBe(["ManageOrders", "ViewDashboard"]);
    }

    // z8rp3fchq3
    [Test]
    public async Task RejectUser_BansAccount_AndLoginIsBlocked()
    {
        var user = await AddUser("pending");
        await new RejectUserCommandHandler(_db, Mock.Of<ISellerHubService>(), Mock.Of<ISender>(), Admin)
            .Handle(new RejectUserCommand { UserId = user.Id, Reason = "Fake documents" }, CancellationToken.None);

        user.Status.ShouldBe("Banned");
        var login = await new LoginCommandHandler(_db, Mock.Of<IJwtService>()).Handle(new LoginCommand(user.Email!, "Passw0rd!"), CancellationToken.None);
        login.Success.ShouldBeFalse();
    }

    // z8rp3fcghv
    [Test]
    public async Task AssignDispute_AlreadyAssignedToAnotherAdmin_Throws()
    {
        var dispute = new Dispute { OrderId = 1, RaisedBy = 1, Type = "INAD", Status = DisputeStatuses.Open, AssignedTo = 7 };
        _db.Disputes.Add(dispute);
        await _db.SaveChangesAsync();

        var handler = new AssignDisputeCommandHandler(_db, Admin, NullLogger<AssignDisputeCommandHandler>.Instance);
        var ex = await Should.ThrowAsync<Exception>(() => handler.Handle(new AssignDisputeCommand(dispute.Id), CancellationToken.None));
        ex.Message.ShouldContain("already assigned");
        dispute.AssignedTo.ShouldBe(7);
    }

    // z8rp3fcghu
    [Test]
    public async Task DisputeDocket_SortByPriority_UsesSeverity()
    {
        foreach (var p in new[] { "Low", "Critical", "Medium", "High" })
            _db.Disputes.Add(new Dispute { OrderId = 1, RaisedBy = 1, Type = "INAD", Status = DisputeStatuses.Open, Priority = p });
        await _db.SaveChangesAsync();

        var page = await new GetDisputeDocketQueryHandler(_db, Admin).Handle(
            new GetDisputeDocketQuery { SortBy = "Priority", Descending = true }, CancellationToken.None);
        page.Items.Select(d => d.Priority).ShouldBe(["Critical", "High", "Medium", "Low"]);
    }

    // z8rp3fcghf
    [Test]
    public void FeeAndPayoutConfig_OutOfRange_AreInvalid()
    {
        new UpdatePlatformFeesCommandValidator().Validate(new UpdatePlatformFeesCommand { ListingFee = -1, FinalValueFeePercentage = 101 })
            .Errors.Count.ShouldBe(2);
        new UpdatePayoutConfigCommandValidator().Validate(new UpdatePayoutConfigCommand("Hourly", -5, 24, true))
            .Errors.Count.ShouldBe(3);
        new UpdatePayoutConfigCommandValidator().Validate(new UpdatePayoutConfigCommand("Weekly", 0, 23, true))
            .IsValid.ShouldBeTrue();
    }

    // z8rp3fcgj8
    [Test]
    public async Task PayoutEngine_StaleProcessingWithoutLockedFunds_IsSkippedNotThrown()
    {
        var seller = await AddUser("seller", "Seller");
        _db.SellerWallets.Add(new SellerWallet { SellerId = seller.Id, LockedBalance = 0 });
        var stale = new PayoutTransaction { SellerId = seller.Id, Amount = 50, Status = PayoutTransaction.StatusProcessing,
            BankSnapshot = "x", CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        _db.PayoutTransactions.Add(stale);
        await _db.SaveChangesAsync();

        var gateway = new Mock<IMockPaymentGateway>();
        var handler = new RunPayoutEngineCommandHandler(_db, gateway.Object, NullLogger<RunPayoutEngineCommandHandler>.Instance,
            Mock.Of<IEncryptionService>(), Mock.Of<ISensitiveDataLogger>());

        await Should.NotThrowAsync(() => handler.Handle(new RunPayoutEngineCommand(), CancellationToken.None));
        stale.Status.ShouldBe(PayoutTransaction.StatusProcessing);
        gateway.VerifyNoOtherCalls();
    }

    // z8rp3fcgj0
    [Test]
    public async Task SellerPendingFunds_ExcludesRefunded_SortsBySettlementAscending()
    {
        var seller = await AddUser("seller", "Seller");
        var product = new Product { Title = "Lamp", SellerId = seller.Id };
        _db.Products.Add(product);
        var now = DateTime.UtcNow;
        foreach (var (status, days) in new[] { ("Delivered", 9), ("Refunded", 1), ("Delivered", 2), ("PartiallyRefunded", 5) })
            _db.OrderTables.Add(new OrderTable { Status = status, SellerEarnings = 10, EstimatedSettlementDate = now.AddDays(days),
                OrderItems = { new OrderItem { Product = product, Quantity = 1 } } });
        await _db.SaveChangesAsync();

        var funds = await new GetSellerPendingFundsQueryHandler(_db).Handle(new GetSellerPendingFundsQuery(seller.Id), CancellationToken.None);
        funds.Select(f => f.Status).ShouldBe(["Delivered", "PartiallyRefunded", "Delivered"]);
    }

    // z8rp3fcghh
    [Test]
    public async Task ScheduledBroadcast_PastTimeRejected_DueOneIsSent()
    {
        var handler = new SendBroadcastCommandHandler(_db, Mock.Of<INotificationNotifier>(), Mock.Of<IServiceScopeFactory>(),
            NullLogger<SendBroadcastCommandHandler>.Instance, Admin);
        await Should.ThrowAsync<ArgumentException>(() => handler.Handle(
            new SendBroadcastCommand { Title = "t", Content = "c", ScheduleAt = DateTime.UtcNow.AddMinutes(-1) }, CancellationToken.None));

        var due = new Notification { Title = "t", Content = "c", Type = "InApp", Status = "Scheduled", ScheduledAt = DateTime.UtcNow.AddMinutes(-1) };
        var later = new Notification { Title = "t", Content = "c", Type = "InApp", Status = "Scheduled", ScheduledAt = DateTime.UtcNow.AddHours(1) };
        _db.Notifications.AddRange(due, later);
        await _db.SaveChangesAsync();

        var notifier = new Mock<INotificationNotifier>();
        (await ScheduledBroadcastService.SendDueAsync(_db, notifier.Object, Mock.Of<IEmailService>(), NullLogger.Instance, CancellationToken.None)).ShouldBe(1);
        due.Status.ShouldBe("Sent");
        due.SentAt.ShouldNotBeNull();
        later.Status.ShouldBe("Scheduled");
        notifier.Verify(n => n.NotifyNewNotificationAsync(due.Id, "t", "c", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // z8rp3fcgjf
    [Test]
    public async Task OrderStats_RefundedOrdersCountAsReturned()
    {
        foreach (var s in new[] { "Refunded", "PartiallyRefunded", "Returned", "Completed" })
            _db.OrderTables.Add(new OrderTable { Status = s, OrderDate = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var stats = await new GetOrderStatsQueryHandler(_db).Handle(new GetOrderStatsQuery(null, null), CancellationToken.None);
        stats.Returned.ShouldBe(3);
    }

    // z8rp3fcgje
    [Test]
    public async Task DashboardWeeklyRevenue_SumsFeesNotOrderValue()
    {
        _db.OrderTables.Add(new OrderTable { Status = "Delivered", OrderDate = DateTime.UtcNow, TotalPrice = 100 });
        _db.FinancialTransactions.Add(new FinancialTransaction { Type = "FeeDeduction", Amount = 5, Date = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var metrics = await new GetDashboardMetricsQueryHandler(_db).Handle(new GetDashboardMetricsQuery(), CancellationToken.None);
        metrics.WeeklyRevenue.Sum(d => d.Revenue).ShouldBe(5);
    }

    // z8rp3fcx2b
    [Test]
    public async Task Orders_SearchByProductName_AndCanceledMatchesBothSpellings()
    {
        var lamp = new Product { Title = "Vintage Lamp" };
        _db.OrderTables.AddRange(
            new OrderTable { Status = "Cancelled", OrderItems = { new OrderItem { Product = lamp, Quantity = 1 } } },
            new OrderTable { Status = "Canceled" },
            new OrderTable { Status = "Delivered" });
        await _db.SaveChangesAsync();
        var handler = new GetOrdersWithPaginationQueryHandler(_db);

        (await handler.Handle(new GetOrdersWithPaginationQuery { Search = "lamp", PageNumber = 1, PageSize = 10 }, CancellationToken.None)).TotalCount.ShouldBe(1);
        (await handler.Handle(new GetOrdersWithPaginationQuery { Status = "Canceled", PageNumber = 1, PageSize = 10 }, CancellationToken.None)).TotalCount.ShouldBe(2);
    }

    // z8rp3fcghe
    [Test]
    public async Task AuditLogs_DateOnlyToDate_IncludesWholeDay()
    {
        var day = DateTime.UtcNow.Date.AddDays(-2);
        _db.AdminActions.Add(new AdminAction { AdminId = 42, Action = "X", CreatedAt = day.AddHours(15) });
        await _db.SaveChangesAsync();

        var page = await new GetAuditLogsQueryHandler(_db).Handle(new GetAuditLogsQuery { FromDate = day, ToDate = day }, CancellationToken.None);
        page.TotalCount.ShouldBe(1);
    }
}
