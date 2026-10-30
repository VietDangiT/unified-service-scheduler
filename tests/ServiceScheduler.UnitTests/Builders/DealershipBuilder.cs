using System.Globalization;

using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>
/// Riverside by default: Europe/London; Mon-Fri 08:00-18:00, Sat 09:00-13:00, closed Sunday.
/// Tests override only the values that matter to them.
/// </summary>
internal sealed class DealershipBuilder
{
    public static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private readonly Dictionary<DayOfWeek, LocalTimeRange> _openingHours = new()
    {
        [DayOfWeek.Monday] = Hours("08:00", "18:00"),
        [DayOfWeek.Tuesday] = Hours("08:00", "18:00"),
        [DayOfWeek.Wednesday] = Hours("08:00", "18:00"),
        [DayOfWeek.Thursday] = Hours("08:00", "18:00"),
        [DayOfWeek.Friday] = Hours("08:00", "18:00"),
        [DayOfWeek.Saturday] = Hours("09:00", "13:00"),
    };

    private TimeZoneInfo _timeZone = London;

    public static DealershipBuilder Default() => new();

    public DealershipBuilder InTimeZone(string ianaId)
    {
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        return this;
    }

    public DealershipBuilder OpenOn(DayOfWeek day, string opens, string closes)
    {
        _openingHours[day] = Hours(opens, closes);
        return this;
    }

    public DealershipBuilder ClosedOn(DayOfWeek day)
    {
        _openingHours.Remove(day);
        return this;
    }

    public Dealership Build() =>
        new(new DealershipId(Guid.Parse("d0000000-0000-0000-0000-000000000001")), _timeZone, new WeeklySchedule(_openingHours));

    private static LocalTimeRange Hours(string opens, string closes) =>
        new(TimeOnly.Parse(opens, CultureInfo.InvariantCulture), TimeOnly.Parse(closes, CultureInfo.InvariantCulture));
}
