using System.Net;
using System.Text;
using System.Text.Json;

using Npgsql;

using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// The request and response are written with literal property names on purpose: these tests pin the wire format,
// so they do not reference the Api contract types.
public sealed class PostAppointmentTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    // A Tuesday. The seeded oil change takes 60 minutes.
    private static readonly DateTime TuesdayTen = new(2026, 11, 3, 10, 0, 0);
    private static readonly DateTime TuesdayEleven = new(2026, 11, 3, 11, 0, 0);
    private static readonly DateTime TuesdayTwelve = new(2026, 11, 3, 12, 0, 0);

    // Added by the test setup, not the migration seed. A different vehicle keeps the capacity tests about the
    // technician and bay: the same vehicle at an overlapping time is a different rule (rule 8).
    private static readonly Guid SecondVehicleId = new("ca700000-0000-0000-0000-000000000002");

    public async Task InitializeAsync()
    {
        await fixture.DeleteAppointmentsAsync();
        await AddSecondVehicleAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // EC-72: the straight path, end to end. With a single seeded technician and bay, the allocation is unambiguous.
    [Fact]
    public async Task PostAppointment_WhenTechnicianAndBayAreFree_ReturnsCreatedAndStoresConfirmedAppointment()
    {
        using HttpClient client = fixture.CreateClient();
        using var request = new StringContent(
            $$"""
            {
              "customerId": "{{SeedData.CustomerId}}",
              "vehicleId": "{{SeedData.VehicleId}}",
              "serviceTypeId": "{{SeedData.OilChangeServiceTypeId}}",
              "dealershipId": "{{SeedData.DealershipId}}",
              "requestedStart": "2026-11-03T10:00:00"
            }
            """,
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await client.PostAsync(new Uri("/api/appointments", UriKind.Relative), request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;
        Guid appointmentId = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, appointmentId);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal(SeedData.CustomerId, body.GetProperty("customerId").GetGuid());
        Assert.Equal(SeedData.VehicleId, body.GetProperty("vehicleId").GetGuid());
        Assert.Equal(SeedData.OilChangeServiceTypeId, body.GetProperty("serviceTypeId").GetGuid());
        Assert.Equal(SeedData.DealershipId, body.GetProperty("dealershipId").GetGuid());

        // The raw strings, so these also pin that no offset is returned (ADR-0002 #25).
        Assert.Equal("2026-11-03T10:00:00", body.GetProperty("start").GetString());
        Assert.Equal("2026-11-03T11:00:00", body.GetProperty("end").GetString());

        Assert.Equal(SeedData.TechnicianId, body.GetProperty("technician").GetProperty("id").GetGuid());
        Assert.Equal(SeedData.ServiceBayId, body.GetProperty("serviceBay").GetProperty("id").GetGuid());

        // Resolved against the client's base address, so a relative and an absolute Location are treated alike.
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(client.BaseAddress!, response.Headers.Location);
        Assert.Equal($"/api/appointments/{appointmentId}", location.AbsolutePath);

        AppointmentRow? row = await ReadAppointmentRowAsync(appointmentId);
        Assert.NotNull(row);
        Assert.Equal(1, row.RowsInTable);
        Assert.Equal(SeedData.CustomerId, row.CustomerId);
        Assert.Equal(SeedData.VehicleId, row.VehicleId);
        Assert.Equal(SeedData.OilChangeServiceTypeId, row.ServiceTypeId);
        Assert.Equal(SeedData.DealershipId, row.DealershipId);
        Assert.Equal(SeedData.TechnicianId, row.TechnicianId);
        Assert.Equal(SeedData.ServiceBayId, row.ServiceBayId);
        Assert.Equal(TuesdayTen, row.StartAt);
        Assert.Equal(TuesdayEleven, row.EndAt);
        Assert.Equal(row.EndAt, row.BlockedUntil); // the oil change has no buffer
        Assert.Equal("Confirmed", row.Status);
    }

    // EC-73: one technician and one bay, both busy at exactly the requested time. The technician error wins (ADR-0002 #17).
    [Fact]
    public async Task PostAppointment_WhenTechnicianAndBayAreBusyAtTheSameTime_ReturnsNoQualifiedTechnicianAndStoresNothing()
    {
        using HttpClient client = fixture.CreateClient();
        Guid firstAppointmentId = await BookSeededVehicleAtTenAsync(client);

        using HttpResponseMessage response = await PostAppointmentAsync(client, SecondVehicleId, "2026-11-03T10:00:00");

        await AssertNoQualifiedTechnicianProblemAsync(response);
        await AssertOnlyFirstAppointmentIsStoredAsync(firstAppointmentId);
    }

    // EC-02: the request starts while the first job is running, so they overlap for part of the duration.
    [Fact]
    public async Task PostAppointment_WhenTechnicianAndBayAreBusyForPartOfTheDuration_ReturnsNoQualifiedTechnicianAndStoresNothing()
    {
        using HttpClient client = fixture.CreateClient();
        Guid firstAppointmentId = await BookSeededVehicleAtTenAsync(client);

        using HttpResponseMessage response = await PostAppointmentAsync(client, SecondVehicleId, "2026-11-03T10:30:00");

        await AssertNoQualifiedTechnicianProblemAsync(response);
        await AssertOnlyFirstAppointmentIsStoredAsync(firstAppointmentId);
    }

    // EC-01: half-open intervals, so a job starting exactly when the first one ends does not overlap it.
    [Fact]
    public async Task PostAppointment_WhenRequestStartsAsThePreviousAppointmentEnds_ReturnsCreatedOnTheSameTechnicianAndBay()
    {
        using HttpClient client = fixture.CreateClient();
        Guid firstAppointmentId = await BookSeededVehicleAtTenAsync(client);

        using HttpResponseMessage response = await PostAppointmentAsync(client, SecondVehicleId, "2026-11-03T11:00:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        AppointmentRow? first = await ReadAppointmentRowAsync(firstAppointmentId);
        Assert.NotNull(first);
        Assert.Equal(2, first.RowsInTable);
        AssertIsFirstAppointment(first);

        AppointmentRow? second = await ReadAppointmentRowAsync(await ReadAppointmentIdAsync(response));
        Assert.NotNull(second);
        Assert.Equal(SecondVehicleId, second.VehicleId);
        Assert.Equal(SeedData.TechnicianId, second.TechnicianId);
        Assert.Equal(SeedData.ServiceBayId, second.ServiceBayId);
        Assert.Equal(TuesdayEleven, second.StartAt);
        Assert.Equal(TuesdayTwelve, second.EndAt);
        Assert.Equal("Confirmed", second.Status);
    }

    // The seeded vehicle takes the only technician and the only bay, 10:00-11:00.
    private static async Task<Guid> BookSeededVehicleAtTenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await PostAppointmentAsync(client, SeedData.VehicleId, "2026-11-03T10:00:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAppointmentIdAsync(response);
    }

    private static async Task<HttpResponseMessage> PostAppointmentAsync(HttpClient client, Guid vehicleId, string requestedStart)
    {
        using var request = new StringContent(
            $$"""
            {
              "customerId": "{{SeedData.CustomerId}}",
              "vehicleId": "{{vehicleId}}",
              "serviceTypeId": "{{SeedData.OilChangeServiceTypeId}}",
              "dealershipId": "{{SeedData.DealershipId}}",
              "requestedStart": "{{requestedStart}}"
            }
            """,
            Encoding.UTF8,
            "application/json");

        return await client.PostAsync(new Uri("/api/appointments", UriKind.Relative), request);
    }

    private static async Task<Guid> ReadAppointmentIdAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertNoQualifiedTechnicianProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NoQualifiedTechnicianAvailable", document.RootElement.GetProperty("code").GetString());
    }

    private async Task AssertOnlyFirstAppointmentIsStoredAsync(Guid firstAppointmentId)
    {
        AppointmentRow? first = await ReadAppointmentRowAsync(firstAppointmentId);
        Assert.NotNull(first);
        Assert.Equal(1, first.RowsInTable);
        AssertIsFirstAppointment(first);
    }

    private static void AssertIsFirstAppointment(AppointmentRow row)
    {
        Assert.Equal(SeedData.VehicleId, row.VehicleId);
        Assert.Equal(SeedData.TechnicianId, row.TechnicianId);
        Assert.Equal(SeedData.ServiceBayId, row.ServiceBayId);
        Assert.Equal(TuesdayTen, row.StartAt);
        Assert.Equal(TuesdayEleven, row.EndAt);
        Assert.Equal("Confirmed", row.Status);
    }

    // Idempotent because the fixture's database outlives each test and appointments are the only rows reset.
    private async Task AddSecondVehicleAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO vehicles (id, customer_id, vin, make, model, model_year)
            VALUES (@id, @customerId, '2HGBH41JXMN109187', 'Toyota', 'Corolla', 2022)
            ON CONFLICT (id) DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("id", SecondVehicleId);
        command.Parameters.AddWithValue("customerId", SeedData.CustomerId);

        await command.ExecuteNonQueryAsync();
    }

    // Reads the database directly, not through the DbContext, so the check does not share the code under test.
    private async Task<AppointmentRow?> ReadAppointmentRowAsync(Guid appointmentId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT customer_id, vehicle_id, service_type_id, dealership_id, technician_id, service_bay_id,
                   start_at, end_at, blocked_until, status,
                   (SELECT count(*) FROM appointments) AS rows_in_table
            FROM appointments
            WHERE id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", appointmentId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AppointmentRow(
            CustomerId: reader.GetGuid(0),
            VehicleId: reader.GetGuid(1),
            ServiceTypeId: reader.GetGuid(2),
            DealershipId: reader.GetGuid(3),
            TechnicianId: reader.GetGuid(4),
            ServiceBayId: reader.GetGuid(5),
            StartAt: reader.GetDateTime(6),
            EndAt: reader.GetDateTime(7),
            BlockedUntil: reader.GetDateTime(8),
            Status: reader.GetString(9),
            RowsInTable: reader.GetInt64(10));
    }

    private sealed record AppointmentRow(
        Guid CustomerId,
        Guid VehicleId,
        Guid ServiceTypeId,
        Guid DealershipId,
        Guid TechnicianId,
        Guid ServiceBayId,
        DateTime StartAt,
        DateTime EndAt,
        DateTime BlockedUntil,
        string Status,
        long RowsInTable);
}
