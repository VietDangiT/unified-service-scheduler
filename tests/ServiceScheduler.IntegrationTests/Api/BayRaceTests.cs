using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Two General technicians and the one seeded bay, so a race can be lost on the bay alone. A class of its own, with
// its own fixture and database, so the second technician never changes what the other classes allocate.
public sealed class BayRaceTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    // A Tuesday. The seeded oil change takes 60 minutes.
    private const string TuesdayTen = "2026-11-03T10:00:00";
    private static readonly DateTime TuesdayTenAt = new(2026, 11, 3, 10, 0, 0);
    private static readonly DateTime TuesdayElevenAt = new(2026, 11, 3, 11, 0, 0);
    private static readonly DateTime MondayNine = new(2026, 11, 2, 9, 0, 0);

    // Added by the test setup, not the migration seed. The ID sorts after the seeded technician's, so with equal
    // skills and workload the seeded technician is the one chosen (Allocation policy 2).
    private static readonly Guid SecondTechnicianId = new("7ec40000-0000-0000-0000-000000000002");
    private static readonly Guid RequestVehicleId = new("ca700000-0000-0000-0002-000000000001");

    public async Task InitializeAsync()
    {
        await fixture.DeleteAppointmentsAsync();
        await fixture.AddVehicleAsync(RequestVehicleId, "BAYRACE0000000001");
        await AddSecondGeneralTechnicianAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // EC-75: the request chose the free seeded technician and the only bay, which another booking already holds, so
    // its save meets the bay constraint. The constraint name tells which resource conflicted.
    [Fact]
    public async Task PostAppointment_WhenRequestReadBusyIntervalsBeforeTheBayWasTaken_ReturnsNoCompatibleBayAndStoresOneAppointment()
    {
        await InsertAppointmentOnSecondTechnicianAndOnlyBayAtTenAsync();
        await using WebApplicationFactory<Program> staleFactory = fixture.WithStaleBusyIntervals();
        using HttpClient staleClient = staleFactory.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(staleClient, RequestVehicleId, TuesdayTen);

        await AppointmentsApi.AssertConflictAsync(response, "NoCompatibleBayAvailable");
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Idempotent, because the fixture's database outlives each test and only appointments are reset.
    private async Task AddSecondGeneralTechnicianAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO technicians (id, dealership_id, name, is_active)
            VALUES (@id, @dealershipId, 'Bob', true)
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO technician_skills (technician_id, skill)
            VALUES (@id, 'General')
            ON CONFLICT (technician_id, skill) DO NOTHING;

            INSERT INTO technician_shifts (technician_id, day_of_week, starts_at, ends_at)
            VALUES (@id, 2, '08:00', '17:00')
            ON CONFLICT (technician_id, day_of_week) DO NOTHING;
            """,
            connection);
        command.Parameters.AddWithValue("id", SecondTechnicianId);
        command.Parameters.AddWithValue("dealershipId", SeedData.DealershipId);

        await command.ExecuteNonQueryAsync();
    }

    // Written straight to the database: the booking the request did not see. The seeded vehicle, so the request's
    // vehicle and the seeded technician are both free and only the bay conflicts.
    private async Task InsertAppointmentOnSecondTechnicianAndOnlyBayAtTenAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO appointments (id, dealership_id, customer_id, vehicle_id, service_type_id, technician_id,
                                      service_bay_id, start_at, end_at, blocked_until, status, created_at)
            VALUES (@id, @dealershipId, @customerId, @vehicleId, @serviceTypeId, @technicianId,
                    @serviceBayId, @startAt, @endAt, @endAt, 'Confirmed', @createdAt)
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("dealershipId", SeedData.DealershipId);
        command.Parameters.AddWithValue("customerId", SeedData.CustomerId);
        command.Parameters.AddWithValue("vehicleId", SeedData.VehicleId);
        command.Parameters.AddWithValue("serviceTypeId", SeedData.OilChangeServiceTypeId);
        command.Parameters.AddWithValue("technicianId", SecondTechnicianId);
        command.Parameters.AddWithValue("serviceBayId", SeedData.ServiceBayId);
        command.Parameters.AddWithValue("startAt", TuesdayTenAt);
        command.Parameters.AddWithValue("endAt", TuesdayElevenAt);
        command.Parameters.AddWithValue("createdAt", MondayNine);

        await command.ExecuteNonQueryAsync();
    }
}
