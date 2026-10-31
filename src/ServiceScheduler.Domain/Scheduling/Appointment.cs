using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>
/// Aggregate root: a booking of one technician and one bay for one vehicle. References the other aggregates
/// by ID only. "No overlap" spans many appointments, so the database enforces it (ADR-0001).
/// </summary>
public sealed class Appointment
{
    private Appointment(AppointmentId id, BookingRequest request, Allocation allocation, DateTime now)
    {
        Id = id;
        DealershipId = request.Dealership.Id;
        CustomerId = request.CustomerId;
        VehicleId = request.Vehicle.Id;
        ServiceTypeId = request.ServiceType.Id;
        TechnicianId = allocation.TechnicianId;
        ServiceBayId = allocation.BayId;
        Slot = allocation.Slot;
        BlockedUntil = allocation.Slot.End;
        Status = AppointmentStatus.Confirmed;
        CreatedAt = now;
    }

    public AppointmentId Id { get; }

    public DealershipId DealershipId { get; }

    public CustomerId CustomerId { get; }

    public VehicleId VehicleId { get; }

    public ServiceTypeId ServiceTypeId { get; }

    public TechnicianId TechnicianId { get; }

    public ServiceBayId ServiceBayId { get; }

    /// <summary>The service interval; it occupies the vehicle.</summary>
    public TimeSlot Slot { get; }

    /// <summary>
    /// End of the blocked interval, which occupies the technician and bay. Equals the service end while buffers
    /// are parked (ADR-0002, Delivery order).
    /// </summary>
    public DateTime BlockedUntil { get; }

    public AppointmentStatus Status { get; }

    public DateTime CreatedAt { get; }

    public static Appointment Book(AppointmentId id, BookingRequest request, Allocation allocation, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(allocation);

        return new Appointment(id, request, allocation, now);
    }
}
