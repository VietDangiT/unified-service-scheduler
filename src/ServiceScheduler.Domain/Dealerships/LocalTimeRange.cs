namespace ServiceScheduler.Domain.Dealerships;

/// <summary>A half-open wall-clock range <c>[Start, End)</c> within one local day.</summary>
public sealed record LocalTimeRange
{
    /// <exception cref="ArgumentException"><paramref name="end"/> is not after <paramref name="start"/>;
    /// ranges never cross midnight.</exception>
    public LocalTimeRange(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentException($"End {end:HH:mm} must be after start {start:HH:mm}.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }
}
