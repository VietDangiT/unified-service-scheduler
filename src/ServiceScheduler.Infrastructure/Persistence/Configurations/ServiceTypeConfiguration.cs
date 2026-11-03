using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceTypeRow>
{
    public void Configure(EntityTypeBuilder<ServiceTypeRow> builder)
    {
        builder.ToTable("service_types");
        builder.HasKey(serviceType => serviceType.Id).HasName("pk_service_types");
        builder.HasAlternateKey(serviceType => serviceType.Code).HasName("uq_service_types_code");

        builder.Property(serviceType => serviceType.Id).HasColumnName("id");
        builder.Property(serviceType => serviceType.Code).HasColumnName("code");
        builder.Property(serviceType => serviceType.Name).HasColumnName("name");
        builder.Property(serviceType => serviceType.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(serviceType => serviceType.BufferMinutes).HasColumnName("buffer_minutes");
        builder.Property(serviceType => serviceType.RequiredSkill).HasColumnName("required_skill");
        builder.Property(serviceType => serviceType.RequiredBayType).HasColumnName("required_bay_type").HasConversion<string>();

        builder.HasData(new ServiceTypeRow
        {
            Id = SeedData.OilChangeServiceTypeId,
            Code = "oil-change",
            Name = "Oil change",
            DurationMinutes = 60,
            BufferMinutes = 0,
            RequiredSkill = SeedData.GeneralSkill,
            RequiredBayType = BayType.Standard,
        });
        builder.HasData(NorthgateDemoData.ServiceTypes);
    }
}
