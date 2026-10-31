namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class TechnicianShiftRow
{
    public Guid TechnicianId { get; init; }

    public short DayOfWeek { get; init; }

    public TimeOnly StartsAt { get; init; }

    public TimeOnly EndsAt { get; init; }
}
