using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
