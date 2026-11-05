namespace ServiceScheduler.IntegrationTests.Fixtures;

// The ServiceScheduler meter and activity source are process-wide, so a listener also hears the bookings of every other
// test running at the same time. xUnit runs a collection with parallelisation disabled on its own, after the parallel
// ones, so the tests in it count only their own measurements and spans.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialTelemetryTests
{
    public const string Name = "Telemetry";
}
