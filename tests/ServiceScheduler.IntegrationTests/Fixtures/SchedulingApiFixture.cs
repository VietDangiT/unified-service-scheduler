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
/// The real HTTP pipeline against real PostgreSQL, with the schema and seed data from the real migration.
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

    // The tests in a class share this database, so each one starts with no appointments. The seed rows stay.
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

    // A vehicle of the seeded customer. Idempotent, because the database outlives each test and only appointments are reset.
    public async Task AddVehicleAsync(Guid vehicleId, string vin)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO vehicles (id, customer_id, vin, make, model, model_year)
            VALUES (@id, @customerId, @vin, 'Toyota', 'Corolla', 2022)
            ON CONFLICT (id) DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("id", vehicleId);
        command.Parameters.AddWithValue("customerId", SeedData.CustomerId);
        command.Parameters.AddWithValue("vin", vin);

        await command.ExecuteNonQueryAsync();
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
