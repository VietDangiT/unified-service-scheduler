using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>
/// Technician 1 at Riverside by default: active, skills <c>{General}</c>, shifts equal to Riverside's opening
/// hours (Mon-Fri 08:00-18:00, Sat 09:00-13:00). IDs are readable: <c>7ec00000-...-00000000000N</c>.
/// </summary>
internal sealed class TechnicianBuilder
{
    private readonly Dictionary<DayOfWeek, LocalTimeRange> _shifts = new(DealershipBuilder.RiversideOpeningHours);

    private int _number = 1;
    private DealershipId _dealershipId = DealershipBuilder.RiversideId;
    private Skill[] _skills = [Skills.General];
    private bool _isActive = true;

    public static TechnicianBuilder Default() => new();

    public static TechnicianId IdOf(int number) => new(Guid.Parse($"7ec00000-0000-0000-0000-{number:D12}"));

    public TechnicianBuilder WithId(int number)
    {
        _number = number;
        return this;
    }

    public TechnicianBuilder WithSkills(params Skill[] skills)
    {
        _skills = skills;
        return this;
    }

    public TechnicianBuilder OnShift(DayOfWeek day, string start, string end)
    {
        _shifts[day] = DealershipBuilder.Hours(start, end);
        return this;
    }

    public TechnicianBuilder OffShiftOn(DayOfWeek day)
    {
        _shifts.Remove(day);
        return this;
    }

    /// <summary>Replaces every shift.</summary>
    public TechnicianBuilder WithShifts(IReadOnlyDictionary<DayOfWeek, LocalTimeRange> shifts)
    {
        _shifts.Clear();
        foreach (var (day, range) in shifts)
        {
            _shifts[day] = range;
        }

        return this;
    }

    public TechnicianBuilder WithNoShifts()
    {
        _shifts.Clear();
        return this;
    }

    public TechnicianBuilder Inactive()
    {
        _isActive = false;
        return this;
    }

    public TechnicianBuilder AtAnotherDealership()
    {
        _dealershipId = DealershipBuilder.AnotherDealershipId;
        return this;
    }

    public Technician Build() => new(IdOf(_number), _dealershipId, _skills, new WeeklySchedule(_shifts), _isActive);
}
