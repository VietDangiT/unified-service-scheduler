using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Requirement 1: an advisor books a service for a customer's vehicle, and a request the business cannot accept is
// refused with the rule it breaks. "Now" is Monday 2026-11-02 09:00 (the fixture's clock); "Tuesday" is 2026-11-03.
// Each test creates its own dealership with SchedulingDataBuilder and states only what differs from its defaults.
public sealed class BookingRequestTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    private const string TuesdayTen = "2026-11-03T10:00:00";

    private static readonly DateTime TuesdayTenAt = new(2026, 11, 3, 10, 0, 0);
    private static readonly DateTime TuesdayElevenAt = new(2026, 11, 3, 11, 0, 0);

    public async Task InitializeAsync() => await fixture.DeleteAppointmentsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public enum UnknownReference
    {
        Customer,
        Vehicle,
        ServiceType,
        Dealership,
    }

    // Scenario 1, EC-72: the straight path, end to end, with one technician and one bay.
    [Fact]
    public async Task PostAppointment_WhenTechnicianAndBayAreFree_ReturnsCreatedAndStoresTheConfirmedAppointment()
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        Guid vehicleId = data.Customer.VehicleIds[0];
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, vehicleId, TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;
        Guid appointmentId = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, appointmentId);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal(data.Customer.Id, body.GetProperty("customerId").GetGuid());
        Assert.Equal(vehicleId, body.GetProperty("vehicleId").GetGuid());
        Assert.Equal(data.ServiceTypeId, body.GetProperty("serviceTypeId").GetGuid());
        Assert.Equal(data.DealershipId, body.GetProperty("dealershipId").GetGuid());

        // The raw strings, so these also pin that no offset is returned (ADR-0002 #25).
        Assert.Equal("2026-11-03T10:00:00", body.GetProperty("start").GetString());
        Assert.Equal("2026-11-03T11:00:00", body.GetProperty("end").GetString());

        Assert.Equal(data.TechnicianIds[0], body.GetProperty("technician").GetProperty("id").GetGuid());
        Assert.Equal(data.BayIds[0], body.GetProperty("serviceBay").GetProperty("id").GetGuid());

        // Resolved against the client's base address, so a relative and an absolute Location are treated alike.
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(client.BaseAddress!, response.Headers.Location);
        Assert.Equal($"/api/appointments/{appointmentId}", location.AbsolutePath);

        StoredAppointment row = Assert.Single(await fixture.ReadAppointmentsAsync());
        Assert.Equal(appointmentId, row.Id);
        Assert.Equal(data.Customer.Id, row.CustomerId);
        Assert.Equal(vehicleId, row.VehicleId);
        Assert.Equal(data.ServiceTypeId, row.ServiceTypeId);
        Assert.Equal(data.DealershipId, row.DealershipId);
        Assert.Equal(data.TechnicianIds[0], row.TechnicianId);
        Assert.Equal(data.BayIds[0], row.ServiceBayId);
        Assert.Equal(TuesdayTenAt, row.StartAt);
        Assert.Equal(TuesdayElevenAt, row.EndAt);
        Assert.Equal(row.EndAt, row.BlockedUntil); // the oil change has no buffer
        Assert.Equal("Confirmed", row.Status);
    }

    // Scenario 2, EC-19: each of the four references, alone, names something that does not exist (rule 1).
    [Theory]
    [InlineData(UnknownReference.Customer)]
    [InlineData(UnknownReference.Vehicle)]
    [InlineData(UnknownReference.ServiceType)]
    [InlineData(UnknownReference.Dealership)]
    public async Task PostAppointment_WhenAReferenceDoesNotExist_ReturnsNotFoundAndStoresNothing(UnknownReference unknown)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        Guid vehicleId = data.Customer.VehicleIds[0];
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(
            client,
            customerId: unknown == UnknownReference.Customer ? Guid.CreateVersion7() : data.Customer.Id,
            vehicleId: unknown == UnknownReference.Vehicle ? Guid.CreateVersion7() : vehicleId,
            serviceTypeId: unknown == UnknownReference.ServiceType ? Guid.CreateVersion7() : data.ServiceTypeId,
            dealershipId: unknown == UnknownReference.Dealership ? Guid.CreateVersion7() : data.DealershipId,
            TuesdayTen);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, "NotFound");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 3, EC-20: customer A asks for customer B's vehicle (rule 2).
    [Fact]
    public async Task PostAppointment_WhenVehicleBelongsToAnotherCustomer_ReturnsVehicleNotOwnedByCustomerAndStoresNothing()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithCustomer()
            .WithCustomer()
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(
            client, data.Customers[0].Id, data.Customers[1].VehicleIds[0], data.ServiceTypeId, data.DealershipId, TuesdayTen);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "VehicleNotOwnedByCustomer");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 4, EC-22: an EV job where an EV bay exists but no technician holds the EV skill (rule 3).
    [Fact]
    public async Task PostAppointment_WhenNoTechnicianHoldsTheRequiredSkill_ReturnsServiceNotOfferedAtDealershipAndStoresNothing()
    {
        const string evBatteryCheck = "EV battery check";
        SchedulingData data = await new SchedulingDataBuilder()
            .WithServiceType(evBatteryCheck, 60, SchedulingDataBuilder.EvSkill, SchedulingDataBuilder.EvBay)
            .WithTechnicians(1)
            .WithBays(1, SchedulingDataBuilder.EvBay)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(
            client, data, data.Customer.VehicleIds[0], TuesdayTen, data.ServiceTypeIdOf(evBatteryCheck));

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "ServiceNotOfferedAtDealership");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 5, EC-22: the only technician (a) or the only bay (b) is inactive, so the service is not offered at all:
    // a 422 for the dealership, not a 409 for this time (rule 3).
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PostAppointment_WhenTheOnlyTechnicianOrTheOnlyBayIsInactive_ReturnsServiceNotOfferedAtDealershipAndStoresNothing(
        bool technicianIsActive, bool bayIsActive)
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(1, isActive: technicianIsActive)
            .WithBays(1, isActive: bayIsActive)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "ServiceNotOfferedAtDealership");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 6, EC-09: exactly now (a), and an earlier date on a day the dealership is open (b) (rule 4).
    [Theory]
    [InlineData("2026-11-02T09:00:00")]
    [InlineData("2026-10-27T10:00:00")]
    public async Task PostAppointment_WhenStartIsNowOrEarlier_ReturnsStartInPastAndStoresNothing(string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], requestedStart);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "StartInPast");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 7, EC-58: a same-day walk-in. At 09:00 on Monday the advisor books 09:15 today; no minimum notice
    // (ADR-0002 #14).
    [Fact]
    public async Task PostAppointment_WhenStartIsLaterToday_ReturnsCreated()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithOpeningHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))
            .WithTechnician(shifts:
            [
                new WeekdayHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                new WeekdayHours(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
            ])
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], "2026-11-02T09:15:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        StoredAppointment row = Assert.Single(await fixture.ReadAppointmentsAsync());
        Assert.Equal(new DateTime(2026, 11, 2, 9, 15, 0), row.StartAt);
    }

    // Scenario 8, EC-10/31: between quarter hours, or with seconds (rule 6).
    [Theory]
    [InlineData("2026-11-03T10:10:00")]
    [InlineData("2026-11-03T10:00:30")]
    public async Task PostAppointment_WhenStartIsOffTheQuarterHourGrid_ReturnsStartNotOnGridAndStoresNothing(string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], requestedStart);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "StartNotOnGrid");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 8, EC-59: the grid is every quarter hour, not every hour.
    [Theory]
    [InlineData("2026-11-03T10:15:00")]
    [InlineData("2026-11-03T10:45:00")]
    public async Task PostAppointment_WhenStartIsOnAQuarterHour_ReturnsCreated(string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], requestedStart);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        StoredAppointment row = Assert.Single(await fixture.ReadAppointmentsAsync());
        Assert.Equal(DateTime.Parse(requestedStart, CultureInfo.InvariantCulture), row.StartAt);
    }

    // Scenario 9, EC-03/33: before opening, ending after closing (16:15 ends 17:15), and a closed weekday (rule 7).
    [Theory]
    [InlineData("2026-11-03T07:45:00")]
    [InlineData("2026-11-03T16:15:00")]
    [InlineData("2026-11-04T10:00:00")]
    public async Task PostAppointment_WhenServiceIsNotInsideOpeningHours_ReturnsOutsideOpeningHoursAndStoresNothing(string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], requestedStart);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "OutsideOpeningHours");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 9, EC-03: starting at opening and ending at closing both fit (half-open hours).
    [Theory]
    [InlineData("2026-11-03T08:00:00")]
    [InlineData("2026-11-03T16:00:00")]
    public async Task PostAppointment_WhenServiceTouchesOpeningOrClosingTime_ReturnsCreated(string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], requestedStart);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        StoredAppointment row = Assert.Single(await fixture.ReadAppointmentsAsync());
        Assert.Equal(DateTime.Parse(requestedStart, CultureInfo.InvariantCulture), row.StartAt);
    }

    // EC-97: a property the contract does not define, the typo "requestStart", either next to a valid requestedStart
    // (a) or in its place (b). The client's mistake: a 400 problem, nothing stored, nothing logged above Information
    // (api-conventions.md, Security baseline; errors-logging-observability.md).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostAppointment_WhenBodyHasAPropertyTheContractDoesNotDefine_ReturnsBadRequestProblemAndStoresNothing(
        bool alsoSendsRequestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder().BuildAsync(fixture.ConnectionString);
        Guid vehicleId = data.Customer.VehicleIds[0];
        string requestedStart = alsoSendsRequestedStart ? $"\"requestedStart\": \"{TuesdayTen}\"," : string.Empty;
        string body =
            $$"""
            {
              "customerId": "{{data.Customer.Id}}",
              "vehicleId": "{{vehicleId}}",
              "serviceTypeId": "{{data.ServiceTypeId}}",
              "dealershipId": "{{data.DealershipId}}",
              {{requestedStart}}
              "requestStart": "{{TuesdayTen}}"
            }
            """;
        using var logs = new CapturingLoggerProvider();
        await using WebApplicationFactory<Program> factory = fixture.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using HttpClient client = factory.CreateClient();
        using var request = new StringContent(body, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(new Uri("/api/appointments", UriKind.Relative), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AppointmentsApi.ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
        Assert.DoesNotContain(logs.Entries, entry => entry.Level > LogLevel.Information);
    }
}
