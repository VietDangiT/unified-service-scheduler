using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.Domain.Scheduling;

/// <param name="Slot">The validated service interval.</param>
public sealed record AllocationRequest(Dealership Dealership, ServiceType ServiceType, TimeSlot Slot, BusyIntervals Busy);
