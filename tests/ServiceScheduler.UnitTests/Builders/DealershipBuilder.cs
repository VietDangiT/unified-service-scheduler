using System.Globalization;

using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>
/// Riverside by default: Europe/London; Mon-Fri 08:00-18:00, Sat 09:00-13:00, closed Sunday; one active
/// <c>{General}</c> technician whose shifts are the opening hours, and one active <c>Standard</c> bay.
/// Tests override only the values that matter to them.
/// </summary>
internal sealed class DealershipBuilder
{
    public static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static readonly DealershipId RiversideId = new(Guid.Parse("d0000000-0000-0000-0000-000000000001"));

    public static readonly DealershipId AnotherDealershipId = new(Guid.Parse("d0000000-0000-0000-0000-000000000002"));

    /// <summary>Riverside's opening hours; also the default shifts of <see cref="TechnicianBuilder"/>.</summary>
    public static readonly IReadOnlyDictionary<DayOfWeek, LocalTimeRange> RiversideOpeningHours = new Dictionary<DayOfWeek, LocalTimeRange>
    {
        [DayOfWeek.Monday] = Hours("08:00", "18:00"),
        [DayOfWeek.Tuesday] = Hours("08:00", "18:00"),
        [DayOfWeek.Wednesday] = Hours("08:00", "18:00"),
        [DayOfWeek.Thursday] = Hours("08:00", "18:00"),
        [DayOfWeek.Friday] = Hours("08:00", "18:00"),
        [DayOfWeek.Saturday] = Hours("09:00", "13:00"),
    };

    private readonly Dictionary<DayOfWeek, LocalTimeRange> _openingHours = new(RiversideOpeningHours);

    private TimeZoneInfo _timeZone = London;
    private Technician[]? _technicians;
    private ServiceBay[]? _bays;

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

    /// <summary>Replaces the default technician.</summary>
    public DealershipBuilder WithTechnicians(params Technician[] technicians)
    {
        _technicians = technicians;
        return this;
    }

    public DealershipBuilder WithoutTechnicians() => WithTechnicians();

    /// <summary>Replaces the default bay.</summary>
    public DealershipBuilder WithBays(params ServiceBay[] bays)
    {
        _bays = bays;
        return this;
    }

    public DealershipBuilder WithoutBays() => WithBays();

    public Dealership Build() =>
        new(
            RiversideId,
            _timeZone,
            new WeeklySchedule(_openingHours),
            _technicians ?? [TechnicianBuilder.Default().WithShifts(_openingHours).Build()],
            _bays ?? [ServiceBayBuilder.Default().Build()]);

    internal static LocalTimeRange Hours(string opens, string closes) =>
        new(TimeOnly.Parse(opens, CultureInfo.InvariantCulture), TimeOnly.Parse(closes, CultureInfo.InvariantCulture));
}
