using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianSkillConfiguration : IEntityTypeConfiguration<TechnicianSkillRow>
{
    public void Configure(EntityTypeBuilder<TechnicianSkillRow> builder)
    {
        builder.ToTable("technician_skills");
        builder.HasKey(skill => new { skill.TechnicianId, skill.Skill }).HasName("pk_technician_skills");

        builder.Property(skill => skill.TechnicianId).HasColumnName("technician_id");
        builder.Property(skill => skill.Skill).HasColumnName("skill");

        builder.HasOne<TechnicianRow>()
            .WithMany(technician => technician.Skills)
            .HasForeignKey(skill => skill.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_technician_skills_technician");

        builder.HasData(new TechnicianSkillRow { TechnicianId = SeedData.TechnicianId, Skill = SeedData.GeneralSkill });
    }
}
