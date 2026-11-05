using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.UnitTests.Application.Fakes;
using ServiceScheduler.UnitTests.Builders;

namespace ServiceScheduler.UnitTests.Application.Appointments;

// The allocation retries counter shows real contention: one count per retry after a lost race (booking-domain.md,
// Allocation policy 3), never one per attempt, and the spans show each attempt. The instrument and span names are written
// out because dashboards depend on them. The tests run in the serial Telemetry collection, alone, because the meter and
// the activity source are process-wide.
[Collection(SerialTelemetryTests.Name)]
public sealed class BookAppointmentTelemetryTests
{
    private const string AllocationRetries = "scheduler.booking.allocation_retries";
    private const string AllocateSpan = "booking.allocate";
    private const string PersistSpan = "booking.persist";

    // The default booking is Tuesday 3 Nov 2026 10:00, a 60-minute oil change; now is the day before.
    private static readonly DateTime Now = BookingRequestBuilder.TimeOn(new DateOnly(2026, 11, 2), "09:00");

    // EC-94: one lost race is one retry (as EC-18). When every save loses (as EC-44), the three attempts are two retries;
    // the last conflict ends the booking and is not retried. A third free technician means the bound, not a lack of
    // technicians, is what stops it.
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    public async Task HandleAsync_WhenSavesLoseTheRaceOnTheTechnician_CountsOneAllocationRetryPerRetry(int lostRaces, int expectedRetries)
    {
        var dealership = DealershipBuilder.Default()
            .WithTechnicians([.. Enumerable.Range(1, 3).Select(id => TechnicianBuilder.Default().WithId(id).Build())])
            .WithBays([ServiceBayBuilder.Default().WithId(1).Build()])
            .Build();
        var repository = new ScriptedAppointmentRepository(
            [.. Enumerable.Repeat(BookingErrors.NoQualifiedTechnicianAvailable, lostRaces)]);
        using var metrics = new MeasurementRecorder();

        await BookAsync(dealership, repository);

        Assert.Equal(expectedRetries, metrics.SumOf(AllocationRetries));
    }

    // EC-96: each attempt is one allocation and one save, and the save's span says how it ended. The first save loses
    // the race on the technician; the second, with the other technician, adds the appointment.
    [Fact]
    public async Task HandleAsync_WhenTheFirstSaveLosesTheRaceOnTheTechnician_TracesTwoAllocationsAndATechnicianConflictThenAnAddedPersist()
    {
        var dealership = DealershipBuilder.Default()
            .WithTechnicians([TechnicianBuilder.Default().WithId(1).Build(), TechnicianBuilder.Default().WithId(2).Build()])
            .WithBays([ServiceBayBuilder.Default().WithId(1).Build()])
            .Build();
        var repository = new ScriptedAppointmentRepository(BookingErrors.NoQualifiedTechnicianAvailable);
        using var spans = new ActivityRecorder();

        Result<AppointmentDto> result = await BookAsync(dealership, repository);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(2, spans.Named(AllocateSpan).Count);
        Assert.Collection(
            spans.Named(PersistSpan),
            first => Assert.Equal("conflict:technician", first.GetTagItem("outcome")),
            second => Assert.Equal("added", second.GetTagItem("outcome")));
    }

    // Nothing is busy as far as the reader knows, on every read.
    private static Task<Result<AppointmentDto>> BookAsync(Dealership dealership, ScriptedAppointmentRepository repository)
    {
        BookingRequest request = BookingRequestBuilder.Default().AtDealership(dealership).Build();
        var reader = new FakeSchedulingReader(ContextOf(request), BusyIntervalsBuilder.None().Build());
        var allocator = new SlotAllocator(new BestFitBayPolicy(), new BestFitTechnicianPolicy());
        var handler = new BookAppointmentHandler(reader, repository, allocator, new FixedTimeProvider(Now));

        return handler.HandleAsync(CommandOf(request), CancellationToken.None);
    }

    private static BookAppointmentCommand CommandOf(BookingRequest request) =>
        new(
            request.CustomerId.Value,
            request.Vehicle.Id.Value,
            request.ServiceType.Id.Value,
            request.Dealership.Id.Value,
            request.RequestedStart);

    private static BookingContext ContextOf(BookingRequest request) =>
        new(
            request.Vehicle,
            request.ServiceType,
            request.Dealership,
            request.Dealership.Technicians.ToDictionary(technician => technician.Id, technician => $"Technician {technician.Id.Value}"),
            request.Dealership.Bays.ToDictionary(bay => bay.Id, bay => $"Bay {bay.Id.Value}"));
}
