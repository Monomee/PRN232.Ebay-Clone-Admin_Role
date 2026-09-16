using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Stats.Queries;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Stats;

public class DashboardStatsTests
{
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnCorrectStats()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _context.FinancialTransactions.AddRange(new List<FinancialTransaction>
        {
            new FinancialTransaction { Id = 1, Type = "FeeDeduction", Amount = 100, Date = now }, // Daily
            new FinancialTransaction { Id = 2, Type = "FeeDeduction", Amount = 200, Date = now.AddDays(-5) }, // Monthly
            new FinancialTransaction { Id = 3, Type = "FeeDeduction", Amount = 400, Date = now.AddDays(-40) }, // Quarterly
            new FinancialTransaction { Id = 4, Type = "Other", Amount = 1000, Date = now } // Unrelated
        });

        _context.Users.AddRange(new List<User>
        {
            new User { Id = 1, ApprovedAt = now },
            new User { Id = 2, ApprovedAt = now.AddDays(-2) },
            new User { Id = 3, ApprovedAt = now.AddMonths(-2) }
        });

        await _context.SaveChangesAsync();

        var handler = new GetAdminDashboardStatsQueryHandler(_context);
        var query = new GetAdminDashboardStatsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.DailyRevenue.ShouldBe(100);
        result.MonthlyRevenue.ShouldBe(300); // 100 + 200
        result.QuarterlyRevenue.ShouldBe(700); // 100 + 200 + 400
        result.TotalUsers.ShouldBe(3);
        result.NewUsersThisMonth.ShouldBe(2);
    }
}
