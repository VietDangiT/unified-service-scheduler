using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.IntegrationTests.Fixtures;

// A request that read the busy intervals before a competing booking was saved. In a real race that happens only
// sometimes; returning no busy intervals makes it happen every time, so the allocator chooses a resource that is
// already taken and the save meets the exclusion constraint. The booking context and the save stay real.
internal sealed class StaleSchedulingReader(SchedulingReader reader) : ISchedulingReader
{
    public Task<BookingContext?> GetBookingContextAsync(BookAppointmentCommand command, CancellationToken cancellationToken) =>
        reader.GetBookingContextAsync(command, cancellationToken);

    public Task<BusyIntervals> GetBusyIntervalsAsync(DealershipId dealershipId, TimeSlot window, CancellationToken cancellationToken) =>
        Task.FromResult(BusyIntervals.None);
}
