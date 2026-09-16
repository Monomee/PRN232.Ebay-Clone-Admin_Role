using System;

namespace EbayClone.Domain.Entities;

public partial class PlatformFee
{
    public int Id { get; set; }

    public const string TypeFinalValueFee = "FinalValueFee";
    public const string TypeListingFee = "ListingFee";
    public const string TypeSubscriptionFee = "SubscriptionFee";

    public string FeeType { get; set; } = TypeFinalValueFee; // 'ListingFee', 'FinalValueFee', 'SubscriptionFee'

    public int? CategoryId { get; set; } // NULL = applies to all

    public decimal? Percentage { get; set; } // Fee percentage

    public decimal? FixedAmount { get; set; } // Fixed fee

    public decimal? MinAmount { get; set; } // Minimum fee

    public decimal? MaxAmount { get; set; } // Maximum fee

    public bool IsActive { get; set; } = true;

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    // Navigation properties
    public virtual Category? Category { get; set; }
}
