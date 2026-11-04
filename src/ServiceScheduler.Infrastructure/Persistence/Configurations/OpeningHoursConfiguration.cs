using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class OpeningHoursConfiguration : IEntityTypeConfiguration<OpeningHoursRow>
{
    public void Configure(EntityTypeBuilder<OpeningHoursRow> builder)
    {
        builder.ToTable("opening_hours");
        builder.HasKey(hours => new { hours.DealershipId, hours.DayOfWeek }).HasName("pk_opening_hours");

        builder.Property(hours => hours.DealershipId).HasColumnName("dealership_id");
        builder.Property(hours => hours.DayOfWeek).HasColumnName("day_of_week");
        builder.Property(hours => hours.OpensAt).HasColumnName("opens_at").HasColumnType("time");
        builder.Property(hours => hours.ClosesAt).HasColumnName("closes_at").HasColumnType("time");

        builder.HasOne<DealershipRow>()
            .WithMany(dealership => dealership.OpeningHours)
            .HasForeignKey(hours => hours.DealershipId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_opening_hours_dealership");

        builder.HasData(new OpeningHoursRow
        {
            DealershipId = SeedData.DealershipId,
            DayOfWeek = (short)DayOfWeek.Tuesday,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(17, 0),
        });
        builder.HasData(NorthgateDemoData.OpeningHours);
    }
}
