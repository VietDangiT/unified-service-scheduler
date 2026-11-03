namespace ServiceScheduler.UnitTests.Application.Fakes;

// Production reads GetLocalNow().DateTime. Fixing the local zone to UTC makes that return exactly the given
// time, whatever the machine's zone is. This override is the only place the unit tests touch a time zone.
internal sealed class FixedTimeProvider(DateTime now) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
}
