using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBayRow>
{
    public void Configure(EntityTypeBuilder<ServiceBayRow> builder)
    {
        builder.ToTable("service_bays");
        builder.HasKey(bay => bay.Id).HasName("pk_service_bays");
        builder.HasAlternateKey(bay => new { bay.Id, bay.DealershipId }).HasName("uq_service_bays_id_dealership");

        // Every booking loads the dealership's bays (persistence.md, hot-path foreign keys).
        builder.HasIndex(bay => bay.DealershipId).HasDatabaseName("ix_service_bays_dealership_id");

        builder.Property(bay => bay.Id).HasColumnName("id");
        builder.Property(bay => bay.DealershipId).HasColumnName("dealership_id");
        builder.Property(bay => bay.Name).HasColumnName("name");
        builder.Property(bay => bay.BayType).HasColumnName("bay_type").HasConversion<string>();
        builder.Property(bay => bay.IsActive).HasColumnName("is_active");

        builder.HasOne<DealershipRow>()
            .WithMany(dealership => dealership.ServiceBays)
            .HasForeignKey(bay => bay.DealershipId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_service_bays_dealership");

        builder.HasData(new ServiceBayRow
        {
            Id = SeedData.ServiceBayId,
            DealershipId = SeedData.DealershipId,
            Name = "Bay-1",
            BayType = BayType.Standard,
            IsActive = true,
        });
    }
}
