using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Application.Fakes;

// Returns the same busy intervals on every read, so it never sees a booking that won a race. A retry can then only
// avoid the conflicting technician or bay by excluding it, not by reading again.
internal sealed class FakeSchedulingReader(BookingContext context, BusyIntervals busy) : ISchedulingReader
{
    public Task<BookingContext?> GetBookingContextAsync(BookAppointmentCommand command, CancellationToken cancellationToken) =>
        Task.FromResult<BookingContext?>(context);

    public Task<BusyIntervals> GetBusyIntervalsAsync(DealershipId dealershipId, TimeSlot window, CancellationToken cancellationToken) =>
        Task.FromResult(busy);
}
