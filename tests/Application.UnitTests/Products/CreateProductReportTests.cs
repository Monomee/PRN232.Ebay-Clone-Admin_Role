using EbayClone.Application.Products.Commands.CreateProductReport;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.UnitTests.Products;

public class CreateProductReportTests
{
    private ApplicationDbContext _db = null!;

    [SetUp]
    public async Task Setup()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Products.Add(new Product { Id = 1, Title = "Item", SellerId = 9 });
        _db.Users.AddRange(new User { Id = 5, Username = "buyer" }, new User { Id = 6, Username = "pending", ApprovalStatus = "PendingApproval" });
        await _db.SaveChangesAsync();
    }

    [TearDown] public void TearDown() => _db.Dispose();

    private Task<int> Report(int? reporter, string? type = null, string priority = "Low", int productId = 1) =>
        new CreateProductReportCommandHandler(_db).Handle(
            new CreateProductReportCommand { ProductId = productId, ReporterUserId = reporter, ReporterType = type ?? "User", Priority = priority, Reason = "fake" },
            CancellationToken.None);

    // Repeating a report, or asking for "Critical", must not push a listing up the moderation queue.
    [Test]
    public async Task SameReporter_CountsOnce_AndCannotPickPriority()
    {
        var first = await Report(5, priority: "Critical");
        var again = await Report(5, priority: "Critical");

        again.ShouldBe(first);
        (await _db.ProductReports.CountAsync()).ShouldBe(1);
        (await _db.ProductReports.SingleAsync()).Priority.ShouldBe("Low");
        (await _db.Products.SingleAsync()).ReportCount.ShouldBe(1);
    }

    [Test]
    public async Task SystemTypeFromRequest_IsStoredAsUser()
    {
        await Report(5, type: "System");
        (await _db.ProductReports.SingleAsync()).ReporterType.ShouldBe("User");
    }

    [Test]
    public async Task AnonymousPendingOrMissingProduct_AreRejected()
    {
        await Should.ThrowAsync<UnauthorizedAccessException>(() => Report(null));
        await Should.ThrowAsync<ValidationException>(() => Report(6));          // not risk-reviewed yet
        await Should.ThrowAsync<KeyNotFoundException>(() => Report(5, productId: 404));
        (await _db.ProductReports.CountAsync()).ShouldBe(0);
    }
}
