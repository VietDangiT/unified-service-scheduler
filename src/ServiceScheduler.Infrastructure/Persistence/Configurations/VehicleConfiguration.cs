using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<VehicleRow>
{
    public void Configure(EntityTypeBuilder<VehicleRow> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(vehicle => vehicle.Id).HasName("pk_vehicles");
        builder.HasAlternateKey(vehicle => vehicle.Vin).HasName("uq_vehicles_vin");

        builder.Property(vehicle => vehicle.Id).HasColumnName("id");
        builder.Property(vehicle => vehicle.CustomerId).HasColumnName("customer_id");
        builder.Property(vehicle => vehicle.Vin).HasColumnName("vin");
        builder.Property(vehicle => vehicle.Make).HasColumnName("make");
        builder.Property(vehicle => vehicle.Model).HasColumnName("model");
        builder.Property(vehicle => vehicle.ModelYear).HasColumnName("model_year");

        builder.HasOne<CustomerRow>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vehicles_customer");

        builder.HasData(new VehicleRow
        {
            Id = SeedData.VehicleId,
            CustomerId = SeedData.CustomerId,
            Vin = "1HGBH41JXMN109186",
            Make = "Honda",
            Model = "Civic",
            ModelYear = 2021,
        });
    }
}
