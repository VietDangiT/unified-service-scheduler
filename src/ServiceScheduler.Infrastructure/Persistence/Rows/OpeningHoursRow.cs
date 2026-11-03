namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class OpeningHoursRow
{
    public Guid DealershipId { get; init; }

    public short DayOfWeek { get; init; }

    public TimeOnly OpensAt { get; init; }

    public TimeOnly ClosesAt { get; init; }
}
