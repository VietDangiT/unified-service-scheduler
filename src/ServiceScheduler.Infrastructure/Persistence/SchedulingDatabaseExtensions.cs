using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceScheduler.Infrastructure.Persistence;

public static class SchedulingDatabaseExtensions
{
    /// <summary>
    /// Brings the scheduling database up to the latest migration, demo seed included. Meant for a single
    /// instance at startup (the one-command demo); a multi-instance deployment applies migrations as a release step.
    /// </summary>
    public static async Task MigrateSchedulingDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        SchedulingDbContext context = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        await context.Database.MigrateAsync(cancellationToken);
    }
}
