using EbayClone.Application.AdminRoles.Commands.UpdateAdminRole;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

/// Editing a custom role changes what its admins can do (SessionValidation then rejects their old tokens),
/// and only grantable policies are accepted, never SuperAdmin-only ones.
public class UpdateAdminRoleTests
{
    [Test]
    public async Task UpdatedPermissions_AreWhatTheRoleGrants_AndAreAudited()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.AdminRoles.Add(new AdminRole { Id = 5, RoleName = "QA Lead", Permissions = """["ViewDashboard","ModerateReviews"]""" });
        db.AdminUserRoles.Add(new AdminUserRole { UserId = 77, RoleId = 5 });
        await db.SaveChangesAsync();
        var log = new Mock<ISensitiveDataLogger>();
        var handler = new UpdateAdminRoleCommandHandler(db, log.Object, SensitiveLogTests.Actor("42", "SuperAdmin"));

        Assert.That(await handler.Handle(new(5, "Reviews only", ["ViewDashboard", "ViewDashboard"]), default), Is.True);
        Assert.That(await db.GetAdminPermissionsAsync(77, default), Is.EqualTo(new[] { "ViewDashboard" }));
        Assert.That((await db.AdminActions.SingleAsync()).AdminId, Is.EqualTo(42));
        log.Verify(l => l.Add("UpdateAdminRole", "AdminRole", "5", "AdminPrivilege", It.IsAny<object>()), Times.Once);

        Assert.That(await handler.Handle(new(999, null, []), default), Is.False, "unknown role");
    }

    [TestCase("ManageAdminRoles")] // SuperAdmin-only: checked by role, granting it would silently do nothing
    [TestCase("ViewAuditLogs")]
    [TestCase("managedisputes")]
    public void SuperAdminOnlyOrMisspelledPermission_IsRejected(string permission)
    {
        Assert.That(new UpdateAdminRoleCommandValidator().Validate(new UpdateAdminRoleCommand(1, null, [permission])).IsValid, Is.False);
        Assert.That(new UpdateAdminRoleCommandValidator().Validate(new UpdateAdminRoleCommand(1, null, ["ManageDisputes"])).IsValid, Is.True);
    }
}
