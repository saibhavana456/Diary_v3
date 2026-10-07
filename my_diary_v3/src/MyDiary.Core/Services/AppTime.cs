using System;

namespace MyDiary.Core.Services;

/// <summary>
/// Central clock for the application. Always returns time in Indian Standard Time
/// (IST / Asia&#47;Kolkata, UTC+5:30) regardless of the host machine or container
/// timezone.
///
/// Why this exists: on an IIS server configured for the Indian timezone,
/// <see cref="DateTime.Now"/> already returns IST, so everything "just worked".
/// Under Kubernetes the container timezone defaults to UTC, so <c>DateTime.Now</c>
/// is 5 hours 30 minutes behind and every user-facing time (greetings, report
/// headers/watermarks, audit timestamps, scheduled-job "today") was wrong.
///
/// Use <see cref="Now"/> / <see cref="Today"/> wherever the business logic needs
/// the local (Indian) wall-clock time, instead of <c>DateTime.Now</c>. Keep using
/// <c>DateTime.UtcNow</c> for machine-to-machine concerns (session expiry, cache
/// busting) where an absolute instant is what matters.
/// </summary>
public static class AppTime
{
    /// <summary>
    /// IST timezone, resolved once. Windows uses the id "India Standard Time";
    /// Linux/macOS (IANA) use "Asia/Kolkata". We try both so the same build works
    /// on an IIS/Windows host and a Linux Kubernetes pod. As a last resort we
    /// synthesize a fixed UTC+5:30 zone so the app never crashes on a stripped-down
    /// container image that is missing the tz database.
    /// </summary>
    public static TimeZoneInfo IndiaTimeZone { get; } = ResolveIndiaTimeZone();

    /// <summary>Current date and time in IST.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);

    /// <summary>Current date (midnight) in IST.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Current instant as a DateTimeOffset carrying the IST offset (+05:30).</summary>
    public static DateTimeOffset NowOffset => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IndiaTimeZone);

    /// <summary>Convert an arbitrary UTC (or unspecified-as-UTC) instant to IST.</summary>
    public static DateTime ToIndia(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local)
            utc = utc.ToUniversalTime();
        else if (utc.Kind == DateTimeKind.Unspecified)
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, IndiaTimeZone);
    }

    private static TimeZoneInfo ResolveIndiaTimeZone()
    {
        // Windows id first (IIS hosts), then the IANA id (Linux containers).
        foreach (var id in new[] { "India Standard Time", "Asia/Kolkata" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Fallback: fabricate a fixed UTC+5:30 zone (IST has no DST) so the app
        // still shows correct Indian time even if the tz database is unavailable.
        return TimeZoneInfo.CreateCustomTimeZone(
            id: "IST-Fixed",
            baseUtcOffset: TimeSpan.FromMinutes(330),
            displayName: "(UTC+05:30) India Standard Time",
            standardDisplayName: "India Standard Time");
    }
}
