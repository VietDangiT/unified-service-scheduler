using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<TechnicianRow>
{
    public void Configure(EntityTypeBuilder<TechnicianRow> builder)
    {
        builder.ToTable("technicians");
        builder.HasKey(technician => technician.Id).HasName("pk_technicians");
        builder.HasAlternateKey(technician => new { technician.Id, technician.DealershipId }).HasName("uq_technicians_id_dealership");

        // Every booking loads the dealership's technicians (persistence.md, hot-path foreign keys).
        builder.HasIndex(technician => technician.DealershipId).HasDatabaseName("ix_technicians_dealership_id");

        builder.Property(technician => technician.Id).HasColumnName("id");
        builder.Property(technician => technician.DealershipId).HasColumnName("dealership_id");
        builder.Property(technician => technician.Name).HasColumnName("name");
        builder.Property(technician => technician.IsActive).HasColumnName("is_active");

        builder.HasOne<DealershipRow>()
            .WithMany(dealership => dealership.Technicians)
            .HasForeignKey(technician => technician.DealershipId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_technicians_dealership");

        builder.HasData(new TechnicianRow
        {
            Id = SeedData.TechnicianId,
            DealershipId = SeedData.DealershipId,
            Name = "Alice",
            IsActive = true,
        });
    }
}
