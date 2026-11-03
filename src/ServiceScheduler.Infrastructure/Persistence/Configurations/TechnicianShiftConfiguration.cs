using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianShiftConfiguration : IEntityTypeConfiguration<TechnicianShiftRow>
{
    public void Configure(EntityTypeBuilder<TechnicianShiftRow> builder)
    {
        builder.ToTable("technician_shifts");
        builder.HasKey(shift => new { shift.TechnicianId, shift.DayOfWeek }).HasName("pk_technician_shifts");

        builder.Property(shift => shift.TechnicianId).HasColumnName("technician_id");
        builder.Property(shift => shift.DayOfWeek).HasColumnName("day_of_week");
        builder.Property(shift => shift.StartsAt).HasColumnName("starts_at").HasColumnType("time");
        builder.Property(shift => shift.EndsAt).HasColumnName("ends_at").HasColumnType("time");

        builder.HasOne<TechnicianRow>()
            .WithMany(technician => technician.Shifts)
            .HasForeignKey(shift => shift.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_technician_shifts_technician");

        builder.HasData(new TechnicianShiftRow
        {
            TechnicianId = SeedData.TechnicianId,
            DayOfWeek = (short)DayOfWeek.Tuesday,
            StartsAt = new TimeOnly(8, 0),
            EndsAt = new TimeOnly(17, 0),
        });
        builder.HasData(NorthgateDemoData.TechnicianShifts);
    }
}
