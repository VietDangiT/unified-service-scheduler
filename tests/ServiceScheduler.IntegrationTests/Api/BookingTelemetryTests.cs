using System.Diagnostics;
using System.Net;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// The metrics and spans a booking request records, read through the BCL listeners: these are what the dashboard shows.
// The instrument, span and tag names are written out on purpose, because dashboards and alerts depend on them.
// The tests run in the serial Telemetry collection, alone, because the meter and the activity source are process-wide.
[Collection(SerialTelemetryTests.Name)]
public sealed class BookingTelemetryTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    // The fixture's clock reads Monday 2026-11-02 09:00, so both days are in the future and inside the horizon.
    // The builder opens the dealership on Tuesdays only.
    private const string TuesdayTen = "2026-11-03T10:00:00";
    private const string ClosedWednesdayTen = "2026-11-04T10:00:00";

    private const string BookingsCreated = "scheduler.bookings.created";
    private const string BookingsRejected = "scheduler.bookings.rejected";
    private const string BookingDuration = "scheduler.booking.duration";

    private const string AllocateSpan = "booking.allocate";
    private const string PersistSpan = "booking.persist";

    private SchedulingData _data = null!;
    private HttpClient _client = null!;

    private Guid VehicleId => _data.Customer.VehicleIds[0];

    public async Task InitializeAsync()
    {
        // Its own dealership, with one free technician and one free bay.
        _data = await new SchedulingDataBuilder()
            .WithTechnicians(1)
            .WithBays(1)
            .BuildAsync(fixture.ConnectionString);

        // Creating the client starts the host before any listener does, so start-up records nothing a test counts.
        _client = fixture.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // EC-92: a saved booking counts once as created, under its service type, and its duration once as created.
    [Fact]
    public async Task PostAppointment_WhenBookingSucceeds_CountsOneCreatedBookingForItsServiceTypeAndOneCreatedDuration()
    {
        using var metrics = new MeasurementRecorder();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(_client, _data, VehicleId, TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        RecordedMeasurement created = Assert.Single(metrics.Of(BookingsCreated));
        Assert.Equal(1d, created.Value);
        Assert.Equal(_data.ServiceTypeCode, created.TagOf("service_type"));
        RecordedMeasurement duration = Assert.Single(metrics.Of(BookingDuration));
        Assert.Equal("created", duration.TagOf("outcome"));
        Assert.Empty(metrics.Of(BookingsRejected));
    }

    // EC-93: a request on a closed day counts once as rejected, with the error's code as the reason, its duration once as
    // rejected, and never as created.
    [Fact]
    public async Task PostAppointment_WhenDealershipIsClosedThatDay_CountsOneRejectedBookingWithReasonOutsideOpeningHours()
    {
        using var metrics = new MeasurementRecorder();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(_client, _data, VehicleId, ClosedWednesdayTen);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        RecordedMeasurement rejected = Assert.Single(metrics.Of(BookingsRejected));
        Assert.Equal(1d, rejected.Value);
        Assert.Equal("OutsideOpeningHours", rejected.TagOf("reason"));
        RecordedMeasurement duration = Assert.Single(metrics.Of(BookingDuration));
        Assert.Equal("rejected", duration.TagOf("outcome"));
        Assert.Empty(metrics.Of(BookingsCreated));
    }

    // EC-95: a booking that succeeds at the first attempt shows one allocation and one save that added the appointment,
    // both directly under the span of the HTTP request.
    [Fact]
    public async Task PostAppointment_WhenBookingSucceeds_TracesOneAllocateAndOneAddedPersistUnderTheRequestSpan()
    {
        using var spans = new ActivityRecorder();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(_client, _data, VehicleId, TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Activity request = Assert.Single(spans.From(ActivityRecorder.AspNetCoreSource));
        IReadOnlyList<Activity> scheduler = spans.From(ActivityRecorder.ServiceSchedulerSource);
        Activity allocate = Assert.Single(scheduler, span => span.OperationName == AllocateSpan);
        Activity persist = Assert.Single(scheduler, span => span.OperationName == PersistSpan);
        Assert.Equal("added", persist.GetTagItem("outcome"));
        Assert.Equal(request.TraceId, allocate.TraceId);
        Assert.Equal(request.SpanId, allocate.ParentSpanId);
        Assert.Equal(request.TraceId, persist.TraceId);
        Assert.Equal(request.SpanId, persist.ParentSpanId);
    }
}
