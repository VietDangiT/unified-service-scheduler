using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Dealerships;

/// <summary>Aggregate root owning the opening hours, technicians and service bays; compared by <see cref="Id"/>.</summary>
public sealed class Dealership : IEquatable<Dealership>
{
    /// <exception cref="ArgumentException">A technician or bay belongs to another dealership: bad reference
    /// data, rejected here so rule 3 and the allocator never need to filter for it (ADR-0002 #21).</exception>
    public Dealership(
        DealershipId id,
        WeeklySchedule openingHours,
        IEnumerable<Technician> technicians,
        IEnumerable<ServiceBay> bays)
    {
        ArgumentNullException.ThrowIfNull(openingHours);
        ArgumentNullException.ThrowIfNull(technicians);
        ArgumentNullException.ThrowIfNull(bays);

        Id = id;
        OpeningHours = openingHours;
        Technicians = [.. technicians];
        Bays = [.. bays];

        if (Technicians.Any(technician => technician.DealershipId != id))
        {
            throw new ArgumentException($"Every technician must belong to dealership {id.Value}.", nameof(technicians));
        }

        if (Bays.Any(bay => bay.DealershipId != id))
        {
            throw new ArgumentException($"Every service bay must belong to dealership {id.Value}.", nameof(bays));
        }
    }

    public DealershipId Id { get; }

    public WeeklySchedule OpeningHours { get; }

    public IReadOnlyCollection<Technician> Technicians { get; }

    public IReadOnlyCollection<ServiceBay> Bays { get; }

    public bool IsOpenFor(TimeSlot slot) => OpeningHours.Covers(slot);

    public bool Equals(Dealership? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as Dealership);

    public override int GetHashCode() => Id.GetHashCode();
}
