using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

public interface ISchedulingReader
{
    /// <returns>The context, or null when the customer, vehicle, service type or dealership does not exist.</returns>
    Task<BookingContext?> GetBookingContextAsync(BookAppointmentCommand command, CancellationToken cancellationToken);

    /// <returns>The blocked intervals of the dealership's confirmed appointments that overlap <paramref name="window"/>.</returns>
    Task<BusyIntervals> GetBusyIntervalsAsync(DealershipId dealershipId, TimeSlot window, CancellationToken cancellationToken);
}
