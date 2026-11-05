using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace ServiceScheduler.IntegrationTests.Fixtures;

// Records every measurement of the ServiceScheduler meter from the moment it is created until it is disposed. The meter
// is process-wide, so a test using it runs in the serial Telemetry collection, alone, and sees only its own measurements.
// Requests record on pool threads, hence the concurrent collection.
internal sealed class MeasurementRecorder : IDisposable
{
    private const string MeterName = "ServiceScheduler";

    private readonly ConcurrentQueue<RecordedMeasurement> _measurements = new();
    private readonly MeterListener _listener = new();

    public MeasurementRecorder()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();
    }

    /// <summary>The measurements of one instrument, by its published name.</summary>
    public IReadOnlyList<RecordedMeasurement> Of(string instrumentName) =>
        [.. _measurements.Where(measurement => measurement.Instrument == instrumentName)];

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copied = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            copied[tag.Key] = tag.Value;
        }

        _measurements.Enqueue(new RecordedMeasurement(instrument.Name, value, copied));
    }
}

internal sealed record RecordedMeasurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)
{
    public object? TagOf(string key) => Tags.GetValueOrDefault(key);
}
