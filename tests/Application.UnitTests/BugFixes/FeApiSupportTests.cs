using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.AuditLogs.Queries.GetAuditLogSummary;
using EbayClone.Application.Orders.Queries.GetOrderSummary;
using EbayClone.Application.Products.Queries.GetManagedProductSummary;
using EbayClone.Application.Products.Queries.GetProductById;
using EbayClone.Application.Users.Queries.GetUserSummary;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.BugFixes;

/// API side of W8 frontend [BUG]s: page cards must count every row, not only the loaded page.
[TestFixture]
public class FeApiSupportTests
{
    private ApplicationDbContext _db = null!;

    [SetUp] public void SetUp() => _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [TearDown] public void TearDown() => _db.Dispose();

    // z8rp3fchqy
    [Test]
    public async Task UserSummary_CountsAllUsers()
    {
        _db.Users.AddRange(
            new User { Username = "a", Status = "Active", ApprovalStatus = "PendingApproval" },
            new User { Username = "b", Status = "Banned" },
            new User { Username = "c", Status = "Active" });
        await _db.SaveChangesAsync();

        (await new GetUserSummaryQueryHandler(_db).Handle(new GetUserSummaryQuery(), CancellationToken.None))
            .ShouldBe(new UserSummaryDto(3, 1, 1));
    }

    // z8rp3fchtc, z8rp3fchta
    [Test]
    public async Task ManagedProducts_SummaryCountsReportOutcomes_AndSearchIsServerSide()
    {
        _db.Products.AddRange(
            new Product { Title = "Fake Rolex", Reports = { new ProductReport { Status = "Pending" } } },
            new Product { Title = "Lamp", Reports = { new ProductReport { Status = "Resolved" } } },
            new Product { Title = "Desk", Reports = { new ProductReport { Status = "Rejected" }, new ProductReport { Status = "Rejected" } } });
        await _db.SaveChangesAsync();

        (await new GetManagedProductSummaryQueryHandler(_db).Handle(new GetManagedProductSummaryQuery(), CancellationToken.None))
            .ShouldBe(new ManagedProductSummaryDto(1, 1, 1));

        var page = await new GetManagedProductsQueryHandler(_db).Handle(
            new GetManagedProductsQuery { Search = "rolex", PageSize = 1 }, CancellationToken.None);
        page.TotalCount.ShouldBe(1);
        page.Items.Single().Title.ShouldBe("Fake Rolex");
    }

    // z8rp3fcxr1
    [Test]
    public async Task OrderSummary_AggregatesAllOrders()
    {
        _db.OrderTables.AddRange(
            new OrderTable { Status = "Pending", TotalPrice = 10 },
            new OrderTable { Status = "Delivered", TotalPrice = 20 },
            new OrderTable { Status = "Completed", TotalPrice = 30 },
            new OrderTable { Status = "Cancelled", TotalPrice = 40 });
        await _db.SaveChangesAsync();

        (await new GetOrderSummaryQueryHandler(_db).Handle(new GetOrderSummaryQuery(), CancellationToken.None))
            .ShouldBe(new OrderSummaryDto(4, 60, 1, 2, 50));
    }

    // z8rp3fcgjm
    [Test]
    public async Task AuditLogSummary_CountsCriticalAcrossAllEntries()
    {
        _db.AdminActions.AddRange(
            new AdminAction { Action = "BanUser" }, new AdminAction { Action = "DeleteProduct" }, new AdminAction { Action = "ApproveUser" });
        await _db.SaveChangesAsync();

        (await new GetAuditLogSummaryQueryHandler(_db).Handle(new GetAuditLogSummaryQuery(), CancellationToken.None))
            .ShouldBe(new AuditLogSummaryDto(3, 2));
    }

    // z8rp3fcgja
    [Test]
    public async Task ProductDetail_ReturnsSellerPositiveRate()
    {
        var seller = new User { Username = "eva_new" };
        var product = new Product { Title = "Lamp", Seller = seller };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _db.Feedbacks.Add(new Feedback { SellerId = seller.Id, PositiveRate = 74.04m });
        await _db.SaveChangesAsync();

        var dto = await new GetProductByIdQueryHandler(_db).Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);
        dto!.SellerPositiveRate.ShouldBe(74.0m);
    }
}
