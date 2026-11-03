using System.Collections.Frozen;

using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Dealerships;

public sealed class Technician
{
    public Technician(TechnicianId id, DealershipId dealershipId, IEnumerable<Skill> skills, WeeklySchedule shifts, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(shifts);

        Id = id;
        DealershipId = dealershipId;
        Skills = skills.ToFrozenSet();
        Shifts = shifts;
        IsActive = isActive;
    }

    public TechnicianId Id { get; }

    public DealershipId DealershipId { get; }

    public IReadOnlySet<Skill> Skills { get; }

    /// <summary>Working hours per weekday.</summary>
    public WeeklySchedule Shifts { get; }

    public bool IsActive { get; }

    /// <summary>Skills match exactly: the technician needs the required skill itself.</summary>
    public bool IsQualifiedFor(Skill skill) => Skills.Contains(skill);

    public bool IsOnShiftFor(TimeSlot slot) => Shifts.Covers(slot);
}
