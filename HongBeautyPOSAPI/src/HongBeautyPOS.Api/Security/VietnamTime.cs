namespace HongBeautyPOS.Api.Security;

/// <summary>Business days are Asia/Ho_Chi_Minh days; storage is UTC.</summary>
internal static class VietnamTime
{
    private const int MaxRangeDays = 366;
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);

    public static DateTimeOffset StartOfDayUtc(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone));

    /// <summary>Resolves an optional inclusive date range (default: today) into UTC bounds [start, end).</summary>
    public static bool TryResolveRange(DateOnly? from, DateOnly? to, out DateOnly startDate, out DateOnly endDate,
        out DateTimeOffset startUtc, out DateTimeOffset endUtc)
    {
        var today = Today;
        startDate = from ?? today;
        endDate = to ?? today;
        startUtc = endUtc = default;
        if (endDate < startDate || endDate.DayNumber - startDate.DayNumber > MaxRangeDays) return false;
        startUtc = StartOfDayUtc(startDate);
        endUtc = StartOfDayUtc(endDate.AddDays(1));
        return true;
    }
}
