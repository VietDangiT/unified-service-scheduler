namespace ServiceScheduler.UnitTests.Application.Fakes;

// The ServiceScheduler meter is process-wide, so a listener also hears the handlers of every other test running at the
// same time (the lost-race tests retry too). xUnit runs a collection with parallelisation disabled on its own, after
// the parallel ones, so the tests in it count only their own measurements.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialTelemetryTests
{
    public const string Name = "Telemetry";
}
