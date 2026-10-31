using System.Net;
using System.Text;
using System.Text.Json;

using Npgsql;

using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// The request and response are written with literal property names on purpose: these tests pin the wire format,
// so they do not reference the Api contract types.
public sealed class PostAppointmentTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>
{
    // A Tuesday. The seeded oil change takes 60 minutes.
    private static readonly DateTime TuesdayTen = new(2026, 11, 3, 10, 0, 0);
    private static readonly DateTime TuesdayEleven = new(2026, 11, 3, 11, 0, 0);

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
