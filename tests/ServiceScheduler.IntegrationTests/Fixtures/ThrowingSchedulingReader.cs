using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.IntegrationTests.Fixtures;

// A reader port that breaks, as a lost database connection would: every call throws the exception the test supplies,
// so the test can check that the same instance reaches the log and that none of it reaches the client.
internal sealed class ThrowingSchedulingReader(Exception failure) : ISchedulingReader
{
    public Task<BookingContext?> GetBookingContextAsync(BookAppointmentCommand command, CancellationToken cancellationToken) =>
        Task.FromException<BookingContext?>(failure);

    public Task<BusyIntervals> GetBusyIntervalsAsync(DealershipId dealershipId, TimeSlot window, CancellationToken cancellationToken) =>
        Task.FromException<BusyIntervals>(failure);
}
