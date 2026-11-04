using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class AppointmentRow
{
    public Guid Id { get; init; }

    public Guid DealershipId { get; init; }

    public Guid CustomerId { get; init; }

    public Guid VehicleId { get; init; }

    public Guid ServiceTypeId { get; init; }

    public Guid TechnicianId { get; init; }

    public Guid ServiceBayId { get; init; }

    public DateTime StartAt { get; init; }

    public DateTime EndAt { get; init; }

    public DateTime BlockedUntil { get; init; }

    public AppointmentStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
}
