namespace ServiceScheduler.Domain.Scheduling;

/// <summary>A half-open UTC interval <c>[StartUtc, EndUtc)</c>.</summary>
public sealed record TimeSlot
{
    private TimeSlot(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="duration"/> is not positive. Durations come from configured service types,
    /// so this is a programming error, not a business rejection.
    /// </exception>
    public static TimeSlot Create(DateTimeOffset start, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        var startUtc = start.ToUniversalTime();
        return new TimeSlot(startUtc, startUtc + duration);
    }

    // Half-open: slots that only touch (one ends as the other starts) do not overlap.
    public bool Overlaps(TimeSlot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return StartUtc < other.EndUtc && other.StartUtc < EndUtc;
    }
}
