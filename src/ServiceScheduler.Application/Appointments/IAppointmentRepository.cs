using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

public interface IAppointmentRepository
{
    /// <summary>
    /// Saves a new appointment. The database, not the earlier check, is the final guard against double-booking, so the
    /// save fails with a capacity error when a concurrent booking already holds the technician or the bay, and with
    /// <see cref="BookingErrors.VehicleAlreadyBooked"/> when the vehicle already has an overlapping appointment. The
    /// vehicle case is never worth retrying with another technician and bay.
    /// </summary>
    Task<Result<Appointment>> AddAsync(Appointment appointment, CancellationToken cancellationToken);
}
