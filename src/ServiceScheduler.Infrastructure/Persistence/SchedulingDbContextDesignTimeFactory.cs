using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceScheduler.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the model without running the Api, so no connection string is needed to add or
/// script a migration.
/// </summary>
public sealed class SchedulingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SchedulingDbContext>
{
    public SchedulingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SchedulingDbContext>().UseNpgsql().Options);
}
