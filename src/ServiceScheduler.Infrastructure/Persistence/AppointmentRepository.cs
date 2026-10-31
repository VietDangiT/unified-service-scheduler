using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence;

public sealed class AppointmentRepository(SchedulingDbContext db) : IAppointmentRepository
{
    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        db.Appointments.Add(new AppointmentRow
        {
            Id = appointment.Id.Value,
            DealershipId = appointment.DealershipId.Value,
            CustomerId = appointment.CustomerId.Value,
            VehicleId = appointment.VehicleId.Value,
            ServiceTypeId = appointment.ServiceTypeId.Value,
            TechnicianId = appointment.TechnicianId.Value,
            ServiceBayId = appointment.ServiceBayId.Value,
            StartAt = appointment.Slot.Start,
            EndAt = appointment.Slot.End,
            BlockedUntil = appointment.BlockedUntil,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
