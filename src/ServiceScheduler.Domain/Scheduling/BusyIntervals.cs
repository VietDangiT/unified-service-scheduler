using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>Blocked intervals of the confirmed appointments that may affect an allocation, per technician and per bay.</summary>
public sealed record BusyIntervals(
    IReadOnlyCollection<TechnicianBusyInterval> Technicians,
    IReadOnlyCollection<BayBusyInterval> Bays)
{
    public static BusyIntervals None { get; } = new([], []);
}

/// <summary>A technician is occupied for <see cref="Blocked"/> (service plus buffer) by a confirmed appointment.</summary>
public sealed record TechnicianBusyInterval(TechnicianId TechnicianId, TimeSlot Blocked);

/// <summary>A bay is occupied for <see cref="Blocked"/> (service plus buffer) by a confirmed appointment.</summary>
public sealed record BayBusyInterval(ServiceBayId BayId, TimeSlot Blocked);
