using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
namespace EbayClone.Application.UnitTests.Startup;

public class SeederSafetyTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(12)]
    public async Task ExistingDisputesAndWalletsRemainUnchanged(int count)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.AddRange(new User { Id = 1, Username = "john_buyer" }, new User { Id = 2, Username = "tech_seller_pro" }, new User { Id = 3, Username = "fashion_boutique" }, new User { Id = 4, Username = "system" });
        db.DisputeMessages.Add(new DisputeMessage { Id = 1, DisputeId = 1, SenderId = 1, Content = "preserve", SenderType = "Buyer", MessageType = "Note" });
        for (int i = 1; i <= count; i++) db.Disputes.Add(new Dispute { Id = i, Status = "Existing" });
        db.SellerWallets.Add(new SellerWallet { SellerId = 42, PendingBalance = -5, DisputedBalance = 17 });
        await db.SaveChangesAsync();
        await new DisputesSeeder(db, NullLogger<DisputesSeeder>.Instance).SeedAsync();
        Assert.That(await db.Disputes.CountAsync(), Is.EqualTo(count));
        Assert.That(await db.Disputes.AllAsync(x => x.Status == "Existing"), Is.True);
        Assert.That((await db.DisputeMessages.SingleAsync()).Content, Is.EqualTo("preserve"));
        Assert.That(await db.Products.CountAsync(), Is.Zero);
        Assert.That(await db.OrderTables.CountAsync(), Is.Zero);
        var wallet = await db.SellerWallets.SingleAsync();
        Assert.That(wallet.PendingBalance, Is.EqualTo(-5));
        Assert.That(wallet.DisputedBalance, Is.EqualTo(17));
    }
    [Test]
    public async Task ExistingRolePermissionsArePreserved()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.AdminRoles.Add(new AdminRole { RoleName = "Support", Description = "custom", Permissions = "[]" });
        await db.SaveChangesAsync();
        await new AdminRolesSeeder(db, NullLogger<AdminRolesSeeder>.Instance).SeedAsync();
        var role = await db.AdminRoles.SingleAsync(x => x.RoleName == "Support");
        Assert.That(role.Description, Is.EqualTo("custom"));
        Assert.That(role.Permissions, Is.EqualTo("[]"));
        Assert.That(await db.AdminRoles.CountAsync(), Is.EqualTo(3));
    }
}
