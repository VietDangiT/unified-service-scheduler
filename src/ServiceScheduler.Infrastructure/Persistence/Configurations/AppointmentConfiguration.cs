using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<AppointmentRow>
{
    public void Configure(EntityTypeBuilder<AppointmentRow> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(appointment => appointment.Id).HasName("pk_appointments");
        builder.HasIndex(appointment => new { appointment.DealershipId, appointment.StartAt })
            .HasDatabaseName("ix_appointments_dealership_start")
            .HasFilter("status = 'Confirmed'");

        ConfigureColumns(builder);
        ConfigureForeignKeys(builder);
    }

    private static void ConfigureColumns(EntityTypeBuilder<AppointmentRow> builder)
    {
        builder.Property(appointment => appointment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(appointment => appointment.DealershipId).HasColumnName("dealership_id");
        builder.Property(appointment => appointment.CustomerId).HasColumnName("customer_id");
        builder.Property(appointment => appointment.VehicleId).HasColumnName("vehicle_id");
        builder.Property(appointment => appointment.ServiceTypeId).HasColumnName("service_type_id");
        builder.Property(appointment => appointment.TechnicianId).HasColumnName("technician_id");
        builder.Property(appointment => appointment.ServiceBayId).HasColumnName("service_bay_id");
        builder.Property(appointment => appointment.StartAt).HasColumnName("start_at").HasColumnType("timestamp");
        builder.Property(appointment => appointment.EndAt).HasColumnName("end_at").HasColumnType("timestamp");
        builder.Property(appointment => appointment.BlockedUntil).HasColumnName("blocked_until").HasColumnType("timestamp");
        builder.Property(appointment => appointment.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(appointment => appointment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp");
    }

    private static void ConfigureForeignKeys(EntityTypeBuilder<AppointmentRow> builder)
    {
        builder.HasOne<DealershipRow>()
            .WithMany()
            .HasForeignKey(appointment => appointment.DealershipId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_dealership");

        builder.HasOne<CustomerRow>()
            .WithMany()
            .HasForeignKey(appointment => appointment.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_customer");

        builder.HasOne<VehicleRow>()
            .WithMany()
            .HasForeignKey(appointment => appointment.VehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_vehicle");

        builder.HasOne<ServiceTypeRow>()
            .WithMany()
            .HasForeignKey(appointment => appointment.ServiceTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_service_type");

        // Composite, so the database guards the dealership boundary as it does overlap.
        builder.HasOne<TechnicianRow>()
            .WithMany()
            .HasForeignKey(appointment => new { appointment.TechnicianId, appointment.DealershipId })
            .HasPrincipalKey(technician => new { technician.Id, technician.DealershipId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_technician_same_dealership");

        builder.HasOne<ServiceBayRow>()
            .WithMany()
            .HasForeignKey(appointment => new { appointment.ServiceBayId, appointment.DealershipId })
            .HasPrincipalKey(bay => new { bay.Id, bay.DealershipId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_bay_same_dealership");
    }
}
