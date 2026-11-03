namespace ServiceScheduler.Domain.Scheduling;

/// <summary>A half-open interval <c>[Start, End)</c> on the one clock (ADR-0002 #25).</summary>
public sealed record TimeSlot
{
    private TimeSlot(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public TimeSpan Duration => End - Start;

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="duration"/> is not positive. Durations come from configured service types,
    /// so this is a programming error, not a business rejection.
    /// </exception>
    public static TimeSlot Create(DateTime start, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        return new TimeSlot(start, start + duration);
    }

    // Half-open: slots that only touch (one ends as the other starts) do not overlap.
    public bool Overlaps(TimeSlot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Start < other.End && other.Start < End;
    }
}
