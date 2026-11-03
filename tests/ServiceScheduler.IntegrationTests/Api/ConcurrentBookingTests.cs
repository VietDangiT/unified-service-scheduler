using System.Net;
using System.Text.Json;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Twenty advisors book Tuesday 10:00 at the same instant, each for a vehicle of its own, so the technicians and bays are
// the only shared resources. The requests wait at one gate and are released together. Until the retry after a lost race
// (EC-18) is built, a loser may be refused while a pair is still free (ADR-0002 #27), so these tests assert "never more
// bookings than capacity, never a resource twice, never a 500".
public sealed class ConcurrentBookingTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    private const int RacingRequests = 20;
    private const string TuesdayTen = "2026-11-03T10:00:00";

    private static readonly string?[] CapacityCodes = ["NoQualifiedTechnicianAvailable", "NoCompatibleBayAvailable"];

    public async Task InitializeAsync() => await fixture.DeleteAppointmentsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Scenario 24, EC-17: the mandatory concurrency test, for the last free technician and bay. Both are busy for every
    // loser, so the technician error is the one returned (ADR-0002 #17).
    [Fact]
    public async Task PostAppointment_WhenTwentyRequestsRaceForTheOnlyTechnicianAndBay_ReturnsOneCreatedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithCustomer(vehicles: RacingRequests)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        RaceOutcome[] outcomes = await RaceAsync(client, data);

        Assert.DoesNotContain(outcomes, outcome => outcome.Status == HttpStatusCode.InternalServerError);
        Assert.Single(outcomes, outcome => outcome.Status == HttpStatusCode.Created);
        Assert.All(
            outcomes.Where(outcome => outcome.Status != HttpStatusCode.Created),
            outcome =>
            {
                Assert.Equal(HttpStatusCode.Conflict, outcome.Status);
                Assert.Equal(AppointmentsApi.ProblemMediaType, outcome.MediaType);
                Assert.Equal("NoQualifiedTechnicianAvailable", outcome.Code);
            });
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 25, EC-85: three technicians and three bays. Never more bookings than capacity and never a technician or
    // a bay twice; "exactly 3" is not asserted until the retry exists (ADR-0002 #27).
    [Fact]
    public async Task PostAppointment_WhenTwentyRequestsRaceForThreeTechniciansAndThreeBays_ReturnsAtMostThreeCreatedOnDistinctTechniciansAndBaysAndConflictForTheRest()
    {
        const int capacity = 3;
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(capacity)
            .WithBays(capacity)
            .WithCustomer(vehicles: RacingRequests)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        RaceOutcome[] outcomes = await RaceAsync(client, data);

        Assert.DoesNotContain(outcomes, outcome => outcome.Status == HttpStatusCode.InternalServerError);
        RaceOutcome[] created = [.. outcomes.Where(outcome => outcome.Status == HttpStatusCode.Created)];
        Assert.InRange(created.Length, 1, capacity);
        Assert.Distinct(created.Select(outcome => outcome.TechnicianId));
        Assert.Distinct(created.Select(outcome => outcome.ServiceBayId));
        AssertEveryOtherIsACapacityConflict(outcomes);
        Assert.Equal(created.Length, await fixture.CountAppointmentsAsync());
    }

    // Scenario 26, EC-89: two technicians but one bay, so a race can be lost on the bay alone. Which error a loser gets
    // depends on what it read and which constraint it met (ADR-0002 #27).
    [Fact]
    public async Task PostAppointment_WhenTwentyRequestsRaceForTwoTechniciansAndTheOnlyBay_ReturnsOneCreatedAndConflictForTheRestAndStoresOneAppointment()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(2)
            .WithBays(1)
            .WithCustomer(vehicles: RacingRequests)
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        RaceOutcome[] outcomes = await RaceAsync(client, data);

        Assert.DoesNotContain(outcomes, outcome => outcome.Status == HttpStatusCode.InternalServerError);
        Assert.Single(outcomes, outcome => outcome.Status == HttpStatusCode.Created);
        AssertEveryOtherIsACapacityConflict(outcomes);
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    private static void AssertEveryOtherIsACapacityConflict(RaceOutcome[] outcomes) =>
        Assert.All(
            outcomes.Where(outcome => outcome.Status != HttpStatusCode.Created),
            outcome =>
            {
                Assert.Equal(HttpStatusCode.Conflict, outcome.Status);
                Assert.Equal(AppointmentsApi.ProblemMediaType, outcome.MediaType);
                Assert.Contains(outcome.Code, CapacityCodes);
            });

    // One request per vehicle of the first customer, all released by the same gate.
    private static async Task<RaceOutcome[]> RaceAsync(HttpClient client, SchedulingData data)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RaceOutcome>[] requests = [.. data.Customer.VehicleIds.Select(vehicleId => PostWhenReleasedAsync(client, gate.Task, data, vehicleId))];

        gate.SetResult();

        return await Task.WhenAll(requests);
    }

    // Every request waits at the gate, so all of them are released together rather than as each task starts.
    private static async Task<RaceOutcome> PostWhenReleasedAsync(HttpClient client, Task gate, SchedulingData data, Guid vehicleId)
    {
        await gate;

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, vehicleId, TuesdayTen);

        return await ReadOutcomeAsync(response);
    }

    // A 201 carries the allocated technician and bay, a problem body carries a code; a 500 from an unhandled exception
    // is not necessarily JSON.
    private static async Task<RaceOutcome> ReadOutcomeAsync(HttpResponseMessage response)
    {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;

        if (response.StatusCode == HttpStatusCode.Created)
        {
            (Guid technicianId, Guid serviceBayId) = await AppointmentsApi.ReadAllocationAsync(response);

            return new RaceOutcome(response.StatusCode, mediaType, Code: null, technicianId, serviceBayId);
        }

        if (mediaType != AppointmentsApi.ProblemMediaType)
        {
            return new RaceOutcome(response.StatusCode, mediaType, Code: null, TechnicianId: null, ServiceBayId: null);
        }

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? code = problem.RootElement.TryGetProperty("code", out JsonElement value) ? value.GetString() : null;

        return new RaceOutcome(response.StatusCode, mediaType, code, TechnicianId: null, ServiceBayId: null);
    }

    private sealed record RaceOutcome(HttpStatusCode Status, string? MediaType, string? Code, Guid? TechnicianId, Guid? ServiceBayId);
}
