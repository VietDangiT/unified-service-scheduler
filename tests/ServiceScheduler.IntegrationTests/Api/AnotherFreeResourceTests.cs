using System.Net;
using System.Text.Json;

using Npgsql;

using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// A second General technician and a second Standard bay, read through the real reader. A class of its own, with its
// own fixture and database, so the added technician and bay never change what the other classes allocate.
public sealed class AnotherFreeResourceTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    // A Tuesday. The seeded oil change takes 60 minutes.
    private const string TuesdayTen = "2026-11-03T10:00:00";
    private const string TuesdayTenThirty = "2026-11-03T10:30:00";

    // Added by the test setup, not the migration seed. The IDs sort after the seeded ones, so with nothing booked the
    // seeded technician and bay are chosen first (Allocation policy 1-2).
    private static readonly Guid AddedTechnicianId = new("7ec40000-0000-0000-0004-000000000001");
    private static readonly Guid AddedBayId = new("ba900000-0000-0000-0004-000000000001");
    private static readonly Guid SecondVehicleId = new("ca700000-0000-0000-0004-000000000001");

    public async Task InitializeAsync()
    {
        await fixture.DeleteAppointmentsAsync();
        await fixture.AddVehicleAsync(SecondVehicleId, "ANOTHERFREE000001");
        await AddGeneralTechnicianAndStandardBayAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // EC-83: the seeded technician and bay are busy 10:00-11:00, so a 10:30 job overlaps them. Another free technician
    // and bay are chosen (EC-02).
    [Fact]
    public async Task PostAppointment_WhenSeededTechnicianAndBayAreBusyAndAnotherOfEachIsFree_ReturnsCreatedOnTheOtherTechnicianAndBay()
    {
        using HttpClient client = fixture.CreateClient();
        using HttpResponseMessage first = await AppointmentsApi.PostAppointmentAsync(client, SeedData.VehicleId, TuesdayTen);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal((SeedData.TechnicianId, SeedData.ServiceBayId), await ReadAllocationAsync(first));

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, SecondVehicleId, TuesdayTenThirty);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((AddedTechnicianId, AddedBayId), await ReadAllocationAsync(response));
        Assert.Equal((Total: 2L, Confirmed: 2L), await CountAppointmentsByStatusAsync());
    }

    private static async Task<(Guid TechnicianId, Guid ServiceBayId)> ReadAllocationAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;

        return (body.GetProperty("technician").GetProperty("id").GetGuid(),
                body.GetProperty("serviceBay").GetProperty("id").GetGuid());
    }

    // Reads the database directly, not through the DbContext, so the check does not share the code under test.
    private async Task<(long Total, long Confirmed)> CountAppointmentsByStatusAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT count(*), count(*) FILTER (WHERE status = 'Confirmed') FROM appointments",
            connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    // Idempotent, because the fixture's database outlives each test and only appointments are reset.
    private async Task AddGeneralTechnicianAndStandardBayAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO technicians (id, dealership_id, name, is_active)
            VALUES (@technicianId, @dealershipId, 'Carol', true)
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO technician_skills (technician_id, skill)
            VALUES (@technicianId, 'General')
            ON CONFLICT (technician_id, skill) DO NOTHING;

            INSERT INTO technician_shifts (technician_id, day_of_week, starts_at, ends_at)
            VALUES (@technicianId, 2, '08:00', '17:00')
            ON CONFLICT (technician_id, day_of_week) DO NOTHING;

            INSERT INTO service_bays (id, dealership_id, name, bay_type, is_active)
            VALUES (@bayId, @dealershipId, 'Bay-2', 'Standard', true)
            ON CONFLICT (id) DO NOTHING;
            """,
            connection);
        command.Parameters.AddWithValue("technicianId", AddedTechnicianId);
        command.Parameters.AddWithValue("bayId", AddedBayId);
        command.Parameters.AddWithValue("dealershipId", SeedData.DealershipId);

        await command.ExecuteNonQueryAsync();
    }
}
