namespace EMS;

/// <summary>
/// The organization's clock. Attendance days, "today", late arrivals and the midnight demo reset follow this time zone,
/// not the server's (a hosted server may run on US or UTC time). Set by App:TimeZone (IANA or Windows id); default India.
/// Stored timestamps stay in UTC; use <see cref="ToAppTime"/> to show them.
/// </summary>
public static class AppClock
{
    public static TimeZoneInfo Zone { get; private set; } = Find("Asia/Kolkata") ?? TimeZoneInfo.Local;

    public static void Configure(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId) && Find(timeZoneId) is { } zone) Zone = zone;
    }

    /// <summary>Current date and time in the app's time zone.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateTime Today => Now.Date;

    /// <summary>A UTC timestamp shown in the app's time zone.</summary>
    public static DateTime ToAppTime(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>A date and time in the app's time zone, as UTC for storing or querying.</summary>
    public static DateTime AppTimeToUtc(this DateTime appTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(appTime, DateTimeKind.Unspecified), Zone);

    private static TimeZoneInfo? Find(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Windows without ICU knows India as "India Standard Time".
            return id == "Asia/Kolkata" ? Find("India Standard Time") : null;
        }
    }
}
