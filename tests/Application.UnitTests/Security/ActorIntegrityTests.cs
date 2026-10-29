using EbayClone.Application.AdminRoles.Commands.CreateAdminRole;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Users.Commands.ApplySellerPenalty;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class ActorIntegrityTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("not-an-id")]
    public void MissingOrInvalidAuthenticatedActor_IsRejected(string? id)
    {
        Assert.Throws<UnauthorizedAccessException>(() => SensitiveLogTests.Actor(id).GetRequiredActorId());
    }

    [Test]
    public void SystemIdentity_CannotActAsAuthenticatedAdmin()
    {
        var user = new Moq.Mock<IUser>();
        user.SetupGet(x => x.Id).Returns("42");
        user.SetupGet(x => x.IsSystem).Returns(true);
        Assert.Throws<UnauthorizedAccessException>(() => user.Object.GetRequiredActorId());
    }

    [Test]
    public async Task CreateRole_UsesAuthenticatedActorInsteadOfBody()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await new CreateAdminRoleCommandHandler(db, SensitiveLogTests.Actor("42", "SuperAdmin"))
            .Handle(new() { RoleName = "Fixture", CreatedBy = 999 }, default);
        Assert.That((await db.AdminActions.SingleAsync()).AdminId, Is.EqualTo(42));
    }

    [Test]
    public async Task SellerPenalty_RejectsMissingActorBeforeMutation_EvenWithBodyActor()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { Id = 1, PerformanceScore = 100 });
        await db.SaveChangesAsync();
        var handler = new ApplySellerPenaltyCommandHandler(db, SensitiveLogTests.Actor(null));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(1, 10, "Fixture", 999), default));
        Assert.That((await db.Users.SingleAsync()).PerformanceScore, Is.EqualTo(100));
        Assert.That(db.ChangeTracker.HasChanges(), Is.False);
        Assert.That(await db.AdminActions.CountAsync(), Is.Zero);
    }
}
