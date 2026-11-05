using EbayClone.Application.Users.Risk;
using EbayClone.Domain.Entities;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class SellingLimitsTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Limits_ShrinkWithRisk_AndEndWithHistory()
    {
        var fresh = new User { ApprovedAt = Now.AddDays(-5) };
        Assert.That(SellingLimits.For(fresh, Now), Is.EqualTo(SellingLimits.NewAccount));
        Assert.That(SellingLimits.For(new User { ApprovedAt = Now.AddDays(-5), RiskScore = 45 }, Now), Is.EqualTo(SellingLimits.RiskyNewAccount));
        Assert.That(SellingLimits.For(new User { ApprovedAt = Now.AddDays(-120) }, Now), Is.Null, "older than 90 days");
        Assert.That(SellingLimits.For(new User { ApprovedAt = Now.AddDays(-5), SellerLevel = "TopRated" }, Now), Is.Null);
    }

    [Test]
    public void PayoutHold_OnlyForReviewedRiskyAccounts()
    {
        Assert.That(SellingLimits.PayoutHoldOnApproval(new User { RiskScore = 10 }, Now), Is.Null);
        Assert.That(SellingLimits.PayoutHoldOnApproval(new User { RiskScore = 45 }, Now), Is.EqualTo(Now.AddDays(30)));
    }
}
