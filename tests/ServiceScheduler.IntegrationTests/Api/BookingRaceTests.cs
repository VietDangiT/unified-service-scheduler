using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Requests that compete for the seeded technician and bay. Each uses its own vehicle, so the technician and the bay
// are the only shared resources: the same vehicle twice is a different rule (rule 8).
public sealed class BookingRaceTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    private const int RacingRequests = 20;

    // A Tuesday. The seeded oil change takes 60 minutes.
    private const string TuesdayTen = "2026-11-03T10:00:00";

    // Added by the test setup, not the migration seed.
    private static readonly Guid[] RacingVehicleIds =
        [.. Enumerable.Range(1, RacingRequests).Select(number => new Guid($"ca700000-0000-0000-0001-{number:D12}"))];

    public async Task InitializeAsync()
    {
        await fixture.DeleteAppointmentsAsync();

        for (int index = 0; index < RacingVehicleIds.Length; index++)
        {
            await fixture.AddVehicleAsync(RacingVehicleIds[index], $"RACE0000000000{index + 1:D3}");
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // EC-17: the mandatory concurrency test. Twenty advisors book the last free slot at the same instant.
    [Fact]
    public async Task PostAppointment_WhenTwentyRequestsRaceForTheLastSlot_ReturnsOneCreatedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment()
    {
        using HttpClient client = fixture.CreateClient();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RaceOutcome>[] requests = [.. RacingVehicleIds.Select(vehicleId => PostWhenReleasedAsync(client, gate.Task, vehicleId))];

        gate.SetResult();
        RaceOutcome[] outcomes = await Task.WhenAll(requests);

        Assert.DoesNotContain(outcomes, outcome => outcome.Status == HttpStatusCode.InternalServerError);
        Assert.Single(outcomes, outcome => outcome.Status == HttpStatusCode.Created);
        Assert.All(
            outcomes.Where(outcome => outcome.Status != HttpStatusCode.Created),
            outcome =>
            {
                Assert.Equal(HttpStatusCode.Conflict, outcome.Status);
                Assert.Equal("application/problem+json", outcome.MediaType);
                Assert.Equal("NoQualifiedTechnicianAvailable", outcome.Code);
            });
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // EC-74: the second request chose the technician and bay the first one had just taken, so its save meets the
    // technician constraint. No retry yet, so the error names the resource that conflicted (Allocation policy 3).
    [Fact]
    public async Task PostAppointment_WhenRequestReadBusyIntervalsBeforeTheFirstBookingWasSaved_ReturnsNoQualifiedTechnicianAndStoresOneAppointment()
    {
        using HttpClient client = fixture.CreateClient();
        await using WebApplicationFactory<Program> staleFactory = fixture.WithStaleBusyIntervals();
        using HttpClient staleClient = staleFactory.CreateClient();
        using HttpResponseMessage first = await AppointmentsApi.PostAppointmentAsync(client, SeedData.VehicleId, TuesdayTen);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(staleClient, RacingVehicleIds[0], TuesdayTen);

        await AppointmentsApi.AssertConflictAsync(response, "NoQualifiedTechnicianAvailable");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Every request waits at the gate, so all of them are released together rather than as each task starts.
    private static async Task<RaceOutcome> PostWhenReleasedAsync(HttpClient client, Task gate, Guid vehicleId)
    {
        await gate;

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, vehicleId, TuesdayTen);
        string? mediaType = response.Content.Headers.ContentType?.MediaType;

        return new RaceOutcome(response.StatusCode, mediaType, await ReadProblemCodeAsync(response, mediaType));
    }

    // Only a problem body carries a code; a 500 from an unhandled exception is not JSON.
    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response, string? mediaType)
    {
        if (mediaType != "application/problem+json")
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private sealed record RaceOutcome(HttpStatusCode Status, string? MediaType, string? Code);
}
