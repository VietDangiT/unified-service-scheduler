namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class TechnicianRow
{
    public Guid Id { get; init; }

    public Guid DealershipId { get; init; }

    public required string Name { get; init; }

    public bool IsActive { get; init; }

    public ICollection<TechnicianSkillRow> Skills { get; } = [];

    public ICollection<TechnicianShiftRow> Shifts { get; } = [];
}
