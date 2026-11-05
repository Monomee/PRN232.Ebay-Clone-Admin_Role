using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using FluentValidation.Results;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.Users.Risk;

public record SellingLimit(int MaxActiveListings, decimal MaxItemPrice);

/// A new account has to earn its reach (like eBay's selling limits): a stolen or "farmed" account that suddenly
/// lists 200 phones is capped, and a risky one cannot cash out before buyers had time to complain.
public static class SellingLimits
{
    public static readonly TimeSpan NewAccountPeriod = TimeSpan.FromDays(90);
    public static readonly TimeSpan RiskyPayoutHold = TimeSpan.FromDays(30);

    public static readonly SellingLimit NewAccount = new(10, 500m);
    public static readonly SellingLimit RiskyNewAccount = new(3, 100m);

    private static DateTime AccountStart(User u) => u.ApprovedAt ?? u.CreatedAt.UtcDateTime;

    /// null = no limit (established or proven seller).
    public static SellingLimit? For(User seller, DateTime now)
    {
        if (seller.SellerLevel is "TopRated" or "AboveStandard") return null;
        if (now - AccountStart(seller) >= NewAccountPeriod) return null;
        return seller.RiskScore >= RiskAssessment.ReviewThreshold ? RiskyNewAccount : NewAccount;
    }

    /// Set when an admin approves an account that needed a manual risk review (ApproveUser): it cannot cash out
    /// before buyers had time to complain.
    public static DateTime? PayoutHoldOnApproval(User user, DateTime now) =>
        user.RiskScore >= RiskAssessment.ReviewThreshold ? now + RiskyPayoutHold : null;

    /// Throws a 400 when a new listing (or a price change) goes over the seller's current limit.
    public static async Task EnsureWithinAsync(IApplicationDbContext db, User seller, decimal? price, bool newListing, CancellationToken ct)
    {
        if (For(seller, DateTime.UtcNow) is not { } limit) return;
        if (price > limit.MaxItemPrice)
            throw Fail("Price", $"Tài khoản mới chỉ được đăng sản phẩm giá tối đa {limit.MaxItemPrice:0.##}.");
        if (newListing && await db.Products.CountAsync(p => p.SellerId == seller.Id && p.Status == "Active", ct) >= limit.MaxActiveListings)
            throw Fail("Listings", $"Tài khoản mới chỉ được có tối đa {limit.MaxActiveListings} sản phẩm đang bán.");
    }

    private static ValidationException Fail(string field, string message) => new([new ValidationFailure(field, message)]);
}
