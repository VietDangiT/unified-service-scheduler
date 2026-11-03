using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// A request that read the busy intervals before a competing booking was saved (StaleSchedulingReader), so the
// application check chose a taken technician or bay and only the exclusion constraint can refuse it. The constraint
// name tells which resource conflicted. Each test creates its own dealership.
public sealed class StaleBusyIntervalTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    // A Tuesday. The builder's oil change takes 60 minutes.
    private const string TuesdayTen = "2026-11-03T10:00:00";
    private static readonly DateTime TuesdayTenAt = new(2026, 11, 3, 10, 0, 0);

    public async Task InitializeAsync() => await fixture.DeleteAppointmentsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // EC-74: the second request chose the technician and bay the first one had just taken, so its save meets the
    // technician constraint. No retry yet, so the error names the resource that conflicted (Allocation policy 3).
    [Fact]
    public async Task PostAppointment_WhenRequestReadBusyIntervalsBeforeTheFirstBookingWasSaved_ReturnsNoQualifiedTechnicianAndStoresOneAppointment()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(1)
            .WithBays(1)
            .WithCustomer(vehicles: 2)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();
        await using WebApplicationFactory<Program> staleFactory = fixture.WithStaleBusyIntervals();
        using HttpClient staleClient = staleFactory.CreateClient();
        using HttpResponseMessage first = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(staleClient, data, data.Customer.VehicleIds[1], TuesdayTen);

        await AppointmentsApi.AssertConflictAsync(response, "NoQualifiedTechnicianAvailable");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // EC-75: the request chose the free first technician and the only bay, which another booking already holds, so its
    // save meets the bay constraint. With equal skills and workload the stale request chooses the first technician
    // (Allocation policy 2); the other booking holds the second technician and the only bay, for the first vehicle, so
    // the request's vehicle and the first technician are both free and only the bay conflicts.
    [Fact]
    public async Task PostAppointment_WhenRequestReadBusyIntervalsBeforeTheBayWasTaken_ReturnsNoCompatibleBayAndStoresOneAppointment()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(2)
            .WithBays(1)
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 1, bay: 0, TuesdayTenAt)
            .BuildAsync(fixture.ConnectionString);
        await using WebApplicationFactory<Program> staleFactory = fixture.WithStaleBusyIntervals();
        using HttpClient staleClient = staleFactory.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(staleClient, data, data.Customer.VehicleIds[1], TuesdayTen);

        await AppointmentsApi.AssertConflictAsync(response, "NoCompatibleBayAvailable");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }
}
