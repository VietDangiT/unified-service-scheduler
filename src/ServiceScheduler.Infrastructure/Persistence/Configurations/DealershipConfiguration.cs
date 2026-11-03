using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<DealershipRow>
{
    public void Configure(EntityTypeBuilder<DealershipRow> builder)
    {
        builder.ToTable("dealerships");
        builder.HasKey(dealership => dealership.Id).HasName("pk_dealerships");

        builder.Property(dealership => dealership.Id).HasColumnName("id");
        builder.Property(dealership => dealership.Name).HasColumnName("name");

        builder.HasData(new DealershipRow { Id = SeedData.DealershipId, Name = "Riverside" });
        builder.HasData(NorthgateDemoData.Dealership);
    }
}
