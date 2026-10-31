using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>The technician and bay chosen for a service interval.</summary>
public sealed record Allocation(ServiceBayId BayId, TechnicianId TechnicianId, TimeSlot Slot);
