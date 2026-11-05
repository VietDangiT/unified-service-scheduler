using System.Diagnostics;
using System.Diagnostics.Metrics;

using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

/// <summary>
/// The metrics and spans of a booking request (errors-logging-observability.md, Tracing and metrics). The meter and the
/// activity source are BCL types, so recording needs no package; the Api decides whether anything is exported.
/// </summary>
public static class BookingTelemetry
{
    /// <summary>The name of both the meter and the activity source.</summary>
    public const string Name = "ServiceScheduler";

    private const string AttemptTag = "attempt";
    private const string OutcomeTag = "outcome";

    private const string AllocatedOutcome = "allocated";
    private const string AddedOutcome = "added";
    private const string CreatedOutcome = "created";
    private const string RejectedOutcome = "rejected";

    private static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);

    private static readonly Counter<long> BookingsCreated = Meter.CreateCounter<long>(
        "scheduler.bookings.created", description: "Bookings saved.");

    private static readonly Counter<long> BookingsRejected = Meter.CreateCounter<long>(
        "scheduler.bookings.rejected", description: "Booking requests that ended with a domain error, by its code.");

    private static readonly Counter<long> AllocationRetries = Meter.CreateCounter<long>(
        "scheduler.booking.allocation_retries", description: "Retries after a save lost the race for a technician or a bay.");

    private static readonly Histogram<double> BookingDuration = Meter.CreateHistogram<double>(
        "scheduler.booking.duration", unit: "ms", description: "Time to handle one booking request.");

    internal static Activity? StartAllocate(int attempt) =>
        Source.StartActivity("booking.allocate")?.SetTag(AttemptTag, attempt);

    internal static Activity? StartPersist(int attempt) =>
        Source.StartActivity("booking.persist")?.SetTag(AttemptTag, attempt);

    internal static void EndAllocate(Activity? span, Result<Allocation> allocation) =>
        span?.SetTag(OutcomeTag, allocation.IsSuccess ? AllocatedOutcome : allocation.Error.Code);

    internal static void EndPersist(Activity? span, Result<Appointment> saved) =>
        span?.SetTag(OutcomeTag, saved.IsSuccess ? AddedOutcome : ConflictOutcomeOf(saved.Error));

    internal static void RecordRetry() => AllocationRetries.Add(1);

    // The service type's code and the dealership are a small, fixed set, so they are safe tags; customer, vehicle and
    // appointment IDs never are.
    internal static void RecordCreated(string serviceTypeCode, Guid dealershipId, TimeSpan elapsed)
    {
        BookingsCreated.Add(1, new TagList { { "service_type", serviceTypeCode }, { "dealership_id", dealershipId.ToString() } });
        BookingDuration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(OutcomeTag, CreatedOutcome));
    }

    internal static void RecordRejected(DomainError error, TimeSpan elapsed)
    {
        BookingsRejected.Add(1, new KeyValuePair<string, object?>("reason", error.Code));
        BookingDuration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(OutcomeTag, RejectedOutcome));
    }

    // Which constraint stopped the save. Any other error is not expected from a save, so its code is shown as it is.
    private static string ConflictOutcomeOf(DomainError error) =>
        error == BookingErrors.NoQualifiedTechnicianAvailable ? "conflict:technician"
        : error == BookingErrors.NoCompatibleBayAvailable ? "conflict:bay"
        : error == BookingErrors.VehicleAlreadyBooked ? "conflict:vehicle"
        : error.Code;
}
