using System.Globalization;
using System.Net;
using System.Text.Json;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Requirement 2: a booking succeeds only when a compatible bay and a qualified technician are both free for the whole
// service. "Now" is Monday 2026-11-02 09:00; "Tuesday" is 2026-11-03. Each test creates its own dealership; a resource
// the rule must skip is declared first, so the ID tie-break would choose it if the rule were broken.
public sealed class CapacityTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    private const string TuesdayTen = "2026-11-03T10:00:00";
    private const string TuesdayTenThirty = "2026-11-03T10:30:00";
    private const string EvBatteryCheck = "EV battery check";

    private static readonly DateTime TuesdayTenAt = new(2026, 11, 3, 10, 0, 0);

    public async Task InitializeAsync() => await fixture.DeleteAppointmentsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Scenario 10, EC-73/02: one technician and one bay, held 10:00-11:00 by vehicle 1. Vehicle 2 at the same time or
    // overlapping either end finds both busy; the technician error wins (ADR-0002 #17).
    [Theory]
    [InlineData("2026-11-03T10:00:00")]
    [InlineData("2026-11-03T09:30:00")]
    [InlineData("2026-11-03T10:30:00")]
    public async Task PostAppointment_WhenTheOnlyTechnicianAndBayAreBusyForAnyPartOfTheService_ReturnsNoQualifiedTechnicianAndStoresNothing(
        string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, TuesdayTenAt)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[1], requestedStart);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "NoQualifiedTechnicianAvailable");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 10, EC-01: half-open intervals, so a service ending as the other starts, or starting as it ends, fits.
    [Theory]
    [InlineData("2026-11-03T09:00:00")]
    [InlineData("2026-11-03T11:00:00")]
    public async Task PostAppointment_WhenServiceIsBackToBackWithTheOtherAppointment_ReturnsCreatedOnTheSameTechnicianAndBay(
        string requestedStart)
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, TuesdayTenAt)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[1], requestedStart);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[0], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(2, await fixture.CountAppointmentsAsync());
    }

    // Scenario 11, EC-86: a second technician is free, but the only bay is not.
    [Fact]
    public async Task PostAppointment_WhenATechnicianIsFreeButTheOnlyBayIsBusy_ReturnsNoCompatibleBayAndStoresNothing()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(2)
            .WithBays(1)
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, TuesdayTenAt)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[1], TuesdayTen);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "NoCompatibleBayAvailable");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 12, EC-83: T1 and B1 are held 10:00-11:00, so a 10:30 service overlaps them. T1 holds fewer skills and B1
    // has the lower ID, so best fit would choose them both if it did not see them busy; T2's second skill keeps the
    // least-loaded tie-break from choosing T2 on its own.
    [Fact]
    public async Task PostAppointment_WhenBestFitTechnicianAndBayAreBusyAndAnotherOfEachIsFree_ReturnsCreatedOnTheOtherTechnicianAndBay()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnician()
            .WithTechnician(skills: [SchedulingDataBuilder.GeneralSkill, SchedulingDataBuilder.EvSkill])
            .WithBays(2)
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, TuesdayTenAt)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[1], TuesdayTenThirty);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[1], data.BayIds[1]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(2, await fixture.CountAppointmentsAsync());
    }

    // Scenario 13, EC-87: skills match exactly. T1, declared first and with fewer skills, lacks the EV skill.
    [Fact]
    public async Task PostAppointment_WhenEvJobAndOnlyOneTechnicianHoldsTheEvSkill_ReturnsCreatedOnTheEvTechnician()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithServiceType(EvBatteryCheck, 60, SchedulingDataBuilder.EvSkill, SchedulingDataBuilder.EvBay)
            .WithTechnician()
            .WithTechnician(skills: [SchedulingDataBuilder.GeneralSkill, SchedulingDataBuilder.EvSkill])
            .WithBays(1, SchedulingDataBuilder.EvBay)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(
            client, data, data.Customer.VehicleIds[0], TuesdayTen, data.ServiceTypeIdOf(EvBatteryCheck));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[1], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 14, EC-88: T1 works Tuesday 09:00-12:00, T2 08:00-17:00. T1 is declared first, so it is chosen whenever
    // its shift covers the whole service (touching edges included), and T2 otherwise.
    [Theory]
    [InlineData("2026-11-03T08:00:00", 1)]
    [InlineData("2026-11-03T11:30:00", 1)]
    [InlineData("2026-11-03T09:00:00", 0)]
    [InlineData("2026-11-03T11:00:00", 0)]
    public async Task PostAppointment_WhenATechnicianShiftDoesNotCoverTheWholeService_ReturnsCreatedOnTheTechnicianWhoseShiftDoes(
        string requestedStart, int expectedTechnician)
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnician(shifts: [new WeekdayHours(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0))])
            .WithTechnician()
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], requestedStart);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[expectedTechnician], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(response));
        StoredAppointment row = Assert.Single(await fixture.ReadAppointmentsAsync());
        Assert.Equal(DateTime.Parse(requestedStart, CultureInfo.InvariantCulture), row.StartAt);
    }

    // Scenario 15, EC-23/69: the only technician works Mondays only. The service is offered (rule 3 ignores shifts,
    // ADR-0002 #19), so this is a 409 for the time, not a 422.
    [Fact]
    public async Task PostAppointment_WhenTheOnlyTechnicianHasNoShiftThatWeekday_ReturnsNoQualifiedTechnicianAndStoresNothing()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnician(shifts: [new WeekdayHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))])
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "NoQualifiedTechnicianAvailable");
        Assert.Equal(0, await fixture.CountAppointmentsAsync());
    }

    // Scenario 16, EC-63: the inactive technician and bay are declared first, so the ID tie-break would choose them.
    [Fact]
    public async Task PostAppointment_WhenATechnicianAndABayAreInactive_ReturnsCreatedOnTheActiveOnes()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(1, isActive: false)
            .WithTechnicians(1)
            .WithBays(1, isActive: false)
            .WithBays(1)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[1], data.BayIds[1]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 17, EC-05: an EV job needs an EV bay. The only EV bay is held at 10:00; the Standard bay, declared first,
    // is free but cannot take the job, and a qualified technician is free.
    [Fact]
    public async Task PostAppointment_WhenEvJobAndTheOnlyEvBayIsBusy_ReturnsNoCompatibleBayAndDoesNotUseTheStandardBay()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithServiceType(EvBatteryCheck, 60, SchedulingDataBuilder.EvSkill, SchedulingDataBuilder.EvBay)
            .WithTechnicians(2, skills: [SchedulingDataBuilder.GeneralSkill, SchedulingDataBuilder.EvSkill])
            .WithBays(1, SchedulingDataBuilder.StandardBay)
            .WithBays(1, SchedulingDataBuilder.EvBay)
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 1, TuesdayTenAt, serviceType: EvBatteryCheck)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(
            client, data, data.Customer.VehicleIds[1], TuesdayTen, data.ServiceTypeIdOf(EvBatteryCheck));

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "NoCompatibleBayAvailable");
        StoredAppointment row = Assert.Single(await fixture.ReadAppointmentsAsync());
        Assert.Equal(data.BayIds[1], row.ServiceBayId);
    }

    // Scenario 18, EC-13: a cancelled appointment never blocks capacity (invariant 4). Stored directly, as no cancel
    // endpoint exists yet (ADR-0002 #28).
    [Fact]
    public async Task PostAppointment_WhenTheOnlyTechnicianAndBayHoldOnlyACancelledAppointment_ReturnsCreatedOnThem()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, TuesdayTenAt, status: SchedulingDataBuilder.Cancelled)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[1], TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[0], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(response));
        IReadOnlyList<StoredAppointment> rows = await fixture.ReadAppointmentsAsync();
        Assert.Equal(2, rows.Count);
        StoredAppointment confirmed = Assert.Single(rows, row => row.Status == SchedulingDataBuilder.Confirmed);
        Assert.Equal(data.Customer.VehicleIds[1], confirmed.VehicleId);
    }

    // Scenario 19, EC-25: a 50-minute service ends at 10:50, off the grid; the technician and bay take the next job at
    // 11:00.
    [Fact]
    public async Task PostAppointment_WhenServiceTakesFiftyMinutes_ReturnsCreatedEndingAtTenFiftyAndTheResourcesAreBookableAtEleven()
    {
        const string brakeCheck = "Brake check";
        SchedulingData data = await new SchedulingDataBuilder()
            .WithServiceType(brakeCheck, 50)
            .WithCustomer(vehicles: 2)
            .BuildAsync(fixture.ConnectionString);
        Guid brakeCheckId = data.ServiceTypeIdOf(brakeCheck);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage first = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen, brakeCheckId);
        using HttpResponseMessage second = await AppointmentsApi.PostAppointmentAsync(
            client, data, data.Customer.VehicleIds[1], "2026-11-03T11:00:00", brakeCheckId);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using (JsonDocument document = JsonDocument.Parse(await first.Content.ReadAsStringAsync()))
        {
            Assert.Equal("2026-11-03T10:00:00", document.RootElement.GetProperty("start").GetString());
            Assert.Equal("2026-11-03T10:50:00", document.RootElement.GetProperty("end").GetString());
        }

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((data.TechnicianIds[0], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(second));
        IReadOnlyList<StoredAppointment> rows = await fixture.ReadAppointmentsAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateTime(2026, 11, 3, 10, 50, 0), rows[0].EndAt);
    }

    // Scenario 20, EC-14: the same vehicle again at an overlapping time, although another technician and bay are free
    // (ADR-0002 #26).
    [Fact]
    public async Task PostAppointment_WhenTheSameVehicleIsAlreadyBookedAtAnOverlappingTime_ReturnsVehicleAlreadyBookedAndStoresNothing()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(2)
            .WithBays(2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, TuesdayTenAt)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTenThirty);

        await AppointmentsApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "VehicleAlreadyBooked");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }
}
