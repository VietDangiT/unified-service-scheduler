using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

namespace ServiceScheduler.IntegrationTests.Fixtures;

/// <summary>
/// The real HTTP pipeline against real PostgreSQL, with the schema from the real migration. The migration also seeds a
/// demo dealership, which no test uses: each test arranges its own with <see cref="SchedulingDataBuilder"/>.
/// </summary>
public sealed class SchedulingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // The day before the Tuesday the tests book, so requested starts are in the future and inside the horizon.
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0);

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16.15").Build();

    public string ConnectionString => _database.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        DbContextOptions<SchedulingDbContext> options = new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new SchedulingDbContext(options);
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _database.DisposeAsync();
    }

    // The tests in a class share this database, so each one starts with no appointments and the whole-table counts
    // below see only its own. The reference rows of earlier tests stay; they belong to other dealerships.
    public async Task DeleteAppointmentsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("DELETE FROM appointments", connection);
        await command.ExecuteNonQueryAsync();
    }

    // Reads the database directly, not through the DbContext, so the check does not share the code under test.
    public async Task<long> CountAppointmentsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT count(*) FROM appointments", connection);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    // Every stored appointment, oldest start first, read directly for the same reason as the count above.
    public async Task<IReadOnlyList<StoredAppointment>> ReadAppointmentsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT id, customer_id, vehicle_id, service_type_id, dealership_id, technician_id, service_bay_id,
                   start_at, end_at, blocked_until, status
            FROM appointments
            ORDER BY start_at, id
            """,
            connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        var rows = new List<StoredAppointment>();
        while (await reader.ReadAsync())
        {
            rows.Add(new StoredAppointment(
                Id: reader.GetGuid(0),
                CustomerId: reader.GetGuid(1),
                VehicleId: reader.GetGuid(2),
                ServiceTypeId: reader.GetGuid(3),
                DealershipId: reader.GetGuid(4),
                TechnicianId: reader.GetGuid(5),
                ServiceBayId: reader.GetGuid(6),
                StartAt: reader.GetDateTime(7),
                EndAt: reader.GetDateTime(8),
                BlockedUntil: reader.GetDateTime(9),
                Status: reader.GetString(10)));
        }

        return rows;
    }

    // The same API and database, but every request reads the busy intervals as empty, as if it read them before a
    // competing booking was saved. The caller disposes the returned factory.
    public WebApplicationFactory<Program> WithStaleBusyIntervals() =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISchedulingReader>();
            services.AddScoped<SchedulingReader>();
            services.AddScoped<ISchedulingReader, StaleSchedulingReader>();
        }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration: minimal hosting reads the connection string while
        // composing services, before later configuration callbacks run.
        builder.UseSetting("ConnectionStrings:Scheduling", ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });
    }
}

/// <summary>One row of the <c>appointments</c> table, as <see cref="SchedulingApiFixture.ReadAppointmentsAsync"/> read it.</summary>
public sealed record StoredAppointment(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid DealershipId,
    Guid TechnicianId,
    Guid ServiceBayId,
    DateTime StartAt,
    DateTime EndAt,
    DateTime BlockedUntil,
    string Status);
