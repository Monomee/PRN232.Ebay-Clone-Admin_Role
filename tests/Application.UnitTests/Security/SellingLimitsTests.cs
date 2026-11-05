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
        User Reviewed(int daysAgo, int score = 0, string level = "BelowStandard") =>
            new() { ApprovedAt = Now.AddDays(-daysAgo), RiskCheckedAt = Now.AddDays(-daysAgo), RiskScore = score, SellerLevel = level };
        Assert.That(SellingLimits.For(Reviewed(5), Now), Is.EqualTo(SellingLimits.NewAccount));
        Assert.That(SellingLimits.For(Reviewed(5, score: 45), Now), Is.EqualTo(SellingLimits.RiskyNewAccount));
        Assert.That(SellingLimits.For(Reviewed(120), Now), Is.Null, "older than 90 days");
        Assert.That(SellingLimits.For(Reviewed(5, level: "TopRated"), Now), Is.Null);
        Assert.That(SellingLimits.For(new User { CreatedAt = Now.AddDays(-1) }, Now), Is.Null, "seeded/demo seller, never went through sign-up review");
    }

    [Test]
    public void PayoutHold_OnlyForReviewedRiskyAccounts()
    {
        Assert.That(SellingLimits.PayoutHoldOnApproval(new User { RiskScore = 10 }, Now), Is.Null);
        Assert.That(SellingLimits.PayoutHoldOnApproval(new User { RiskScore = 45 }, Now), Is.EqualTo(Now.AddDays(30)));
    }
}
