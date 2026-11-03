using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

public interface IAppointmentRepository
{
    /// <summary>
    /// Saves a new appointment. Fails with a capacity error when a concurrent booking already holds the technician
    /// or the bay, because the database, not the earlier check, is the final guard against double-booking.
    /// </summary>
    Task<Result<Appointment>> AddAsync(Appointment appointment, CancellationToken cancellationToken);
}
