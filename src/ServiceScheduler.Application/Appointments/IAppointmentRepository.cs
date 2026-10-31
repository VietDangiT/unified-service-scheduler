using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

public interface IAppointmentRepository
{
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken);
}
