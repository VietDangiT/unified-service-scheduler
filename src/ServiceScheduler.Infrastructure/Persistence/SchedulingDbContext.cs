using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

using ServiceScheduler.Infrastructure.Persistence.Configurations;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence;

public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options)
{
    internal DbSet<DealershipRow> Dealerships => Set<DealershipRow>();

    internal DbSet<ServiceTypeRow> ServiceTypes => Set<ServiceTypeRow>();

    internal DbSet<CustomerRow> Customers => Set<CustomerRow>();

    internal DbSet<VehicleRow> Vehicles => Set<VehicleRow>();

    internal DbSet<AppointmentRow> Appointments => Set<AppointmentRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Stops EF Core adding an index per foreign key, so each index exists because it was declared on purpose.
        // See docs/system-design.md §6.2.
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DealershipConfiguration());
        modelBuilder.ApplyConfiguration(new OpeningHoursConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceBayConfiguration());
        modelBuilder.ApplyConfiguration(new TechnicianConfiguration());
        modelBuilder.ApplyConfiguration(new TechnicianSkillConfiguration());
        modelBuilder.ApplyConfiguration(new TechnicianShiftConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceTypeConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
    }
}
