namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class TechnicianSkillRow
{
    public Guid TechnicianId { get; init; }

    public required string Skill { get; init; }
}
