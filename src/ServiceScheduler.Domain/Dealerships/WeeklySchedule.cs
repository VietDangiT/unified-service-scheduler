using System.Collections.Frozen;

using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Dealerships;

/// <summary>One local time range per weekday; a missing weekday means closed (or off shift).</summary>
public sealed class WeeklySchedule
{
    private readonly FrozenDictionary<DayOfWeek, LocalTimeRange> _days;

    public WeeklySchedule(IReadOnlyDictionary<DayOfWeek, LocalTimeRange> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        // Copy, so later changes to the caller's dictionary cannot alter the schedule.
        _days = days.ToFrozenDictionary();
    }

    /// <summary>True when the whole slot falls inside that local day's range, evaluated in <paramref name="zone"/>.</summary>
    public bool Covers(TimeSlot slot, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(zone);

        var localStart = TimeZoneInfo.ConvertTime(slot.StartUtc, zone);
        var localEnd = TimeZoneInfo.ConvertTime(slot.EndUtc, zone);

        // Ranges never cross midnight, so a slot ending on another local date cannot fit.
        if (localStart.Date != localEnd.Date || !_days.TryGetValue(localStart.DayOfWeek, out var range))
        {
            return false;
        }

        return range.Start <= TimeOnly.FromTimeSpan(localStart.TimeOfDay)
            && TimeOnly.FromTimeSpan(localEnd.TimeOfDay) <= range.End;
    }
}
