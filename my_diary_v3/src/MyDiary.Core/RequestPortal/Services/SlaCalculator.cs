using RequestPortal.Core.Abstractions;

namespace RequestPortal.Core.Services;

public sealed class SlaCalculator : ISlaCalculator
{
    private readonly IMasterRepo _masters;
    private readonly IRequestRepo _requests;

    public SlaCalculator(IMasterRepo masters, IRequestRepo requests)
    {
        _masters = masters;
        _requests = requests;
    }

    public async Task<DateTime> ComputeDueUtcAsync(long requestTypeId, int levelNo, DateTime startUtc, long? holidayCalendarId, CancellationToken ct = default)
    {
        var workingDays = await _masters.GetSlaWorkingDaysAsync(requestTypeId, levelNo, ct);
        var holidays = holidayCalendarId.HasValue
            ? (await _masters.GetHolidaysAsync(holidayCalendarId.Value, startUtc, startUtc.AddDays(workingDays * 4 + 14), ct)).ToHashSet()
            : new HashSet<DateTime>();

        var d = startUtc;
        var added = 0;
        while (added < workingDays)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
            if (holidays.Contains(d.Date)) continue;
            added++;
        }
        return d;
    }

    /// <summary>
    /// Recompute the SLA due timestamp for a request, accounting for paused intervals.
    /// </summary>
    public async Task<DateTime> RecomputeForRequestAsync(long requestId, CancellationToken ct = default)
    {
        var r = await _requests.GetAsync(requestId, ct) ?? throw new InvalidOperationException($"Request {requestId} not found");
        var pauses = await _requests.GetSlaPausesAsync(requestId, ct);

        // Sum pause durations that have ended (open pauses don't extend until they resume).
        var pausedTotal = TimeSpan.Zero;
        foreach (var p in pauses)
        {
            if (p.ResumedAt.HasValue) pausedTotal += p.ResumedAt.Value - p.PausedAt;
        }

        var baseStart = r.CreatedAt; // simple model: SLA measured from creation (can be from level entry if level history is added)
        var due = await ComputeDueUtcAsync(r.RequestTypeId, r.CurrentLevel, baseStart, null, ct);
        return due.Add(pausedTotal);
    }
}
