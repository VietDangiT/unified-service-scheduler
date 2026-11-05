using System.Collections.Concurrent;
using System.Diagnostics;

namespace ServiceScheduler.IntegrationTests.Fixtures;

// Records every span the ServiceScheduler source and ASP.NET Core start, from creation until disposal. Spans are kept
// when they start, so the request's own span is there even if the host ends it after the client has read the response;
// a span's tags are read from the same object once it has ended. Like the meter, activity sources are process-wide,
// so a test using this runs in the serial Telemetry collection, alone.
internal sealed class ActivityRecorder : IDisposable
{
    public const string ServiceSchedulerSource = "ServiceScheduler";
    public const string AspNetCoreSource = "Microsoft.AspNetCore";

    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ActivityListener _listener;

    public ActivityRecorder()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is ServiceSchedulerSource or AspNetCoreSource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _activities.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>The spans one source started, by the source's name.</summary>
    public IReadOnlyList<Activity> From(string sourceName) =>
        [.. _activities.Where(activity => activity.Source.Name == sourceName)];

    public void Dispose() => _listener.Dispose();
}
