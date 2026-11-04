using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using ServiceScheduler.Application.Appointments;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// What the booking endpoint writes to the log, read by EventId name, level and structured values: that is the log
// contract. The host runs in Development, as WebApplicationFactory sets it, so the 500 and 400 cases hold there too.
// A class of its own, with its own fixture and database, so the approved test classes stay untouched.
public sealed class BookingLogTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime, IDisposable
{
    // The fixture's clock reads Monday 2026-11-02 09:00, so both days are in the future and inside the horizon.
    // The builder opens the dealership on Tuesdays only.
    private const string TuesdayTen = "2026-11-03T10:00:00";
    private const string ClosedWednesdayTen = "2026-11-04T10:00:00";

    private const string AppointmentBooked = "AppointmentBooked";
    private const string BookingRejected = "BookingRejected";

    // The personal data of the customer each test creates, which must never reach a log entry.
    private const string CustomerFullName = "Maria Lopez";
    private const string CustomerEmail = "maria.lopez@example.com";
    private const string CustomerPhone = "+44 20 7946 0958";
    private static readonly string[] CustomerPersonalData = [CustomerFullName, CustomerEmail, CustomerPhone];

    private readonly CapturingLoggerProvider _logs = new();
    private SchedulingData _data = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private Guid VehicleId => _data.Customer.VehicleIds[0];

    public async Task InitializeAsync()
    {
        await fixture.DeleteAppointmentsAsync();

        // Its own dealership, with one technician and one bay, and a customer whose personal data the tests know.
        _data = await new SchedulingDataBuilder()
            .WithTechnicians(1)
            .WithBays(1)
            .WithCustomer(fullName: CustomerFullName, email: CustomerEmail, phone: CustomerPhone)
            .BuildAsync(fixture.ConnectionString);

        _factory = fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILoggerProvider>(_logs)));
        _client = _factory.CreateClient();

        // Creating the client starts the host; its start-up entries are not what these tests are about.
        _logs.Clear();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // xUnit calls this after DisposeAsync, once the hosts that wrote to the provider are gone.
    public void Dispose() => _logs.Dispose();

    // EC-79: a booking is a business event, logged once at Information with the IDs that identify it, and nothing
    // the API logs carries the customer's personal data.
    [Fact]
    public async Task PostAppointment_WhenBookingSucceeds_LogsOneAppointmentBookedWithTheResponseIdsAndNoPersonalData()
    {
        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(_client, _data, VehicleId, TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;

        IReadOnlyList<CapturedLogEntry> entries = _logs.Entries;
        CapturedLogEntry booked = Assert.Single(entries, entry => entry.EventId.Name == AppointmentBooked);
        Assert.Equal(LogLevel.Information, booked.Level);
        Assert.Equal<object?>(body.GetProperty("id").GetGuid(), booked.ValueOf("AppointmentId"));
        Assert.Equal<object?>(body.GetProperty("dealershipId").GetGuid(), booked.ValueOf("DealershipId"));
        Assert.Equal<object?>(body.GetProperty("technician").GetProperty("id").GetGuid(), booked.ValueOf("TechnicianId"));
        Assert.Equal<object?>(body.GetProperty("serviceBay").GetProperty("id").GetGuid(), booked.ValueOf("ServiceBayId"));

        Assert.All(CustomerPersonalData, personalData =>
            Assert.DoesNotContain(entries, entry => entry.Contains(personalData)));
    }

    // EC-80: an expected rejection is a business event too, so it is Information, never Error, and carries the
    // error's code as the reason.
    [Fact]
    public async Task PostAppointment_WhenDealershipIsClosedThatDay_LogsOneBookingRejectedWithOutsideOpeningHoursAndNothingAtError()
    {
        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(_client, _data, VehicleId, ClosedWednesdayTen);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("OutsideOpeningHours", document.RootElement.GetProperty("code").GetString());

        IReadOnlyList<CapturedLogEntry> entries = _logs.Entries;
        CapturedLogEntry rejected = Assert.Single(entries, entry => entry.EventId.Name == BookingRejected);
        Assert.Equal(LogLevel.Information, rejected.Level);
        Assert.Equal<object?>("OutsideOpeningHours", rejected.ValueOf("Reason"));
        Assert.Equal<object?>(_data.DealershipId, rejected.ValueOf("DealershipId"));

        Assert.DoesNotContain(entries, entry => entry.EventId.Name == AppointmentBooked);
        Assert.DoesNotContain(entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // EC-81: a broken port is not a business outcome. The client gets a generic 500 problem with nothing of the
    // exception in it, and the exception is logged exactly once, at Error, across every category.
    [Fact]
    public async Task PostAppointment_WhenReaderThrowsUnexpectedly_ReturnsGenericProblemAndLogsTheExceptionOnceAtError()
    {
        const string marker = "reader-failure-marker-5d1e9";
        var failure = new InvalidOperationException($"Simulated reader failure {marker}");
        await using WebApplicationFactory<Program> failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISchedulingReader>();
                services.AddSingleton<ISchedulingReader>(new ThrowingSchedulingReader(failure));
            }));
        using HttpClient failingClient = failingFactory.CreateClient();
        _logs.Clear();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(failingClient, _data, VehicleId, TuesdayTen);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement body = document.RootElement;
        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(marker, content, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), content, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ThrowingSchedulingReader), content, StringComparison.Ordinal); // in any stack trace

        IReadOnlyList<CapturedLogEntry> entries = _logs.Entries;
        CapturedLogEntry error = Assert.Single(entries, entry => entry.Level == LogLevel.Error);
        Assert.Same(failure, error.Exception);
        Assert.DoesNotContain(entries, entry => entry.EventId.Name is AppointmentBooked or BookingRejected);
    }

    // EC-82: a body that is not JSON is the client's mistake, so it stays a 400 in every environment and is not
    // treated as a failure needing attention.
    [Fact]
    public async Task PostAppointment_WhenBodyIsNotValidJson_ReturnsBadRequestAndLogsNothingAtError()
    {
        using var request = new StringContent("{ \"customerId\": ", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _client.PostAsync(new Uri("/api/appointments", UriKind.Relative), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(_logs.Entries, entry => entry.Level == LogLevel.Error);
    }
}
