using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>Blocked intervals of confirmed appointments; nothing is busy by default.</summary>
internal sealed class BusyIntervalsBuilder
{
    private readonly List<TechnicianBusyInterval> _technicians = [];
    private readonly List<BayBusyInterval> _bays = [];

    public static BusyIntervalsBuilder None() => new();

    public BusyIntervalsBuilder TechnicianBusy(Technician technician, DateTimeOffset start, int minutes)
    {
        _technicians.Add(new TechnicianBusyInterval(technician.Id, TimeSlot.Create(start, TimeSpan.FromMinutes(minutes))));
        return this;
    }

    public BusyIntervalsBuilder BayBusy(ServiceBay bay, DateTimeOffset start, int minutes)
    {
        _bays.Add(new BayBusyInterval(bay.Id, TimeSlot.Create(start, TimeSpan.FromMinutes(minutes))));
        return this;
    }

    public BusyIntervals Build() => new([.. _technicians], [.. _bays]);
}
