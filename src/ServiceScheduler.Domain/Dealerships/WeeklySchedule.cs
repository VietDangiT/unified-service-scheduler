using System.Collections.Frozen;

using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Dealerships;

/// <summary>One time range per weekday; a missing weekday means closed (or off shift).</summary>
public sealed class WeeklySchedule
{
    private readonly FrozenDictionary<DayOfWeek, TimeRange> _days;

    public WeeklySchedule(IReadOnlyDictionary<DayOfWeek, TimeRange> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        // Copy, so later changes to the caller's dictionary cannot alter the schedule.
        _days = days.ToFrozenDictionary();
    }

    /// <summary>True when the whole slot falls inside that day's range.</summary>
    public bool Covers(TimeSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        // Ranges never cross midnight, so a slot ending on another date cannot fit.
        if (slot.Start.Date != slot.End.Date || !_days.TryGetValue(slot.Start.DayOfWeek, out var range))
        {
            return false;
        }

        return range.Start <= TimeOnly.FromDateTime(slot.Start)
            && TimeOnly.FromDateTime(slot.End) <= range.End;
    }
}
