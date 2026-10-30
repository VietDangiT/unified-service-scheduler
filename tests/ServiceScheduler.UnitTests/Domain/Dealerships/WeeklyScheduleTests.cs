using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Domain.Dealerships;

public sealed class WeeklyScheduleTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // Tuesday 3 Nov 2026, 10:00-11:00 UTC.
    private static readonly TimeSlot TuesdayMorning =
        TimeSlot.Create(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

    [Fact]
    public void Covers_WhenCallerLaterChangesTheSourceDictionary_KeepsOriginalHours()
    {
        var days = new Dictionary<DayOfWeek, LocalTimeRange>
        {
            [DayOfWeek.Tuesday] = new(new TimeOnly(8, 0), new TimeOnly(18, 0)),
        };
        var schedule = new WeeklySchedule(days);

        days.Remove(DayOfWeek.Tuesday);

        Assert.True(schedule.Covers(TuesdayMorning, Utc));
    }

    // A slot ending exactly at local midnight belongs to the next date, so no single-day range can hold it.
    [Fact]
    public void Covers_WhenSlotEndsAtMidnight_ReturnsFalse()
    {
        var schedule = new WeeklySchedule(new Dictionary<DayOfWeek, LocalTimeRange>
        {
            [DayOfWeek.Tuesday] = new(new TimeOnly(0, 0), new TimeOnly(23, 59)),
        });
        var lateSlot = TimeSlot.Create(new DateTimeOffset(2026, 11, 3, 23, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

        Assert.False(schedule.Covers(lateSlot, Utc));
    }
}
