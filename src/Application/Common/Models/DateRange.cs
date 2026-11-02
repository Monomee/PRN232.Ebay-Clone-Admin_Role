namespace EbayClone.Application.Common.Models;

/// Date filters from the admin UI. Dates are compared in UTC, and a date-only end date means
/// "through the end of that day" (bug z8rp3fcghe: the last day used to be dropped).
public static class DateRange
{
    /// A value without a kind (e.g. "2026-10-28" from a query string) is taken as UTC, not server-local time.
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value
    };

    /// Exclusive upper bound: filter with `x < ExclusiveEnd(end)`.
    public static DateTime ExclusiveEnd(DateTime end)
    {
        var utc = ToUtc(end);
        return utc.TimeOfDay == TimeSpan.Zero ? utc.AddDays(1) : utc.AddTicks(1);
    }
}
