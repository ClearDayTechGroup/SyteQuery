namespace SyteQuery.Features.Common.Services;

/// <summary>
/// Extension methods for TimeProvider to handle timezone conversions.
/// </summary>
public static class TimeProviderExtensions
{
    /// <summary>
    /// Converts a UTC DateTime to the user's local timezone.
    /// </summary>
    /// <param name="timeProvider">The time provider with the user's timezone</param>
    /// <param name="utcDateTime">A DateTime in UTC</param>
    /// <returns>The DateTime converted to local time</returns>
    public static DateTime ToLocalDateTime(this TimeProvider timeProvider, DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Local)
            return utcDateTime;

        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            // Assume unspecified is UTC for safety
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeProvider.LocalTimeZone);
        return DateTime.SpecifyKind(localTime, DateTimeKind.Local);
    }

    /// <summary>
    /// Converts a local DateTime (from user input) to UTC using the user's timezone.
    /// Use this when the user selects a date/time in the UI and you need to store it as UTC.
    /// </summary>
    /// <param name="timeProvider">The time provider with the user's timezone</param>
    /// <param name="localDateTime">A DateTime representing local time in the user's timezone</param>
    /// <returns>The DateTime converted to UTC</returns>
    public static DateTime ToUtcDateTime(this TimeProvider timeProvider, DateTime localDateTime)
    {
        if (localDateTime.Kind == DateTimeKind.Utc)
            return localDateTime;

        // Treat the input as local time in the browser's timezone
        // MudBlazor pickers return Unspecified kind, so we treat them as local
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(unspecified, timeProvider.LocalTimeZone);
        return DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
    }

    /// <summary>
    /// Converts a nullable UTC DateTime to the user's local timezone.
    /// </summary>
    public static DateTime? ToLocalDateTime(this TimeProvider timeProvider, DateTime? utcDateTime)
    {
        return utcDateTime.HasValue ? timeProvider.ToLocalDateTime(utcDateTime.Value) : null;
    }

    /// <summary>
    /// Converts a nullable local DateTime to UTC.
    /// </summary>
    public static DateTime? ToUtcDateTime(this TimeProvider timeProvider, DateTime? localDateTime)
    {
        return localDateTime.HasValue ? timeProvider.ToUtcDateTime(localDateTime.Value) : null;
    }
}
