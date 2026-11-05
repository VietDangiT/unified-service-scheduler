using System.Collections.Concurrent;
using System.Diagnostics;

namespace ServiceScheduler.UnitTests.Application.Fakes;

// Records every span the ServiceScheduler source starts, in the order they start, from creation until disposal. A
// span's tags are read from the same object once it has ended. The source is process-wide, so a test using this runs in
// the serial Telemetry collection, alone.
internal sealed class ActivityRecorder : IDisposable
{
    private const string SourceName = "ServiceScheduler";

    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ActivityListener _listener;

    public ActivityRecorder()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _activities.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>The spans with this operation name, in the order they started.</summary>
    public IReadOnlyList<Activity> Named(string operationName) =>
        [.. _activities.Where(activity => activity.OperationName == operationName)];

    public void Dispose() => _listener.Dispose();
}
