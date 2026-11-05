namespace EbayClone.Application.Users.Risk;

/// Structure of a 12-digit Vietnamese citizen ID (CCCD): PPP C YY NNNNNN
///   PPP = province of birth registration, C = gender + century, YY = last two digits of the birth year.
/// Catches made-up numbers (the cheapest way to sign up a second account); it cannot prove the card exists,
/// that needs eKYC against the chip or the national database.
public static class CccdFormat
{
    // ponytail: province codes as issued before the 2025 province merger (numbers already issued never change).
    // Add new codes here if the Ministry starts issuing them.
    private static readonly HashSet<string> ProvinceCodes =
    [
        "001", "002", "004", "006", "008", "010", "011", "012", "014", "015", "017", "019", "020", "022", "024", "025",
        "026", "027", "030", "031", "033", "034", "035", "036", "037", "038", "040", "042", "044", "045", "046", "048",
        "049", "051", "052", "054", "056", "058", "060", "062", "064", "066", "067", "068", "070", "072", "074", "075",
        "077", "079", "080", "082", "083", "084", "086", "087", "089", "091", "092", "093", "094", "095", "096",
    ];

    public static bool IsValid(string? cccd, DateOnly dateOfBirth)
    {
        if (cccd is not { Length: 12 } || !cccd.All(char.IsAsciiDigit)) return false;
        if (!ProvinceCodes.Contains(cccd[..3])) return false;
        // Gender/century digit: 0-1 = 1900s, 2-3 = 2000s, 4-5 = 2100s, ... (even = male, odd = female)
        var birthYear = 1900 + (cccd[3] - '0') / 2 * 100 + int.Parse(cccd[4..6]);
        return birthYear == dateOfBirth.Year;
    }
}
