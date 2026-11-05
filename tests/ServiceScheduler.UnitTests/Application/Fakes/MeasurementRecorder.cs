using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace ServiceScheduler.UnitTests.Application.Fakes;

// Records every measurement of the ServiceScheduler meter from the moment it is created until it is disposed. The meter
// is process-wide, so a test using it runs in the serial Telemetry collection, alone, and sees only its own measurements.
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
        _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Record(instrument, value));
        _listener.SetMeasurementEventCallback<int>((instrument, value, _, _) => Record(instrument, value));
        _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Record(instrument, value));
        _listener.Start();
    }

    /// <summary>The sum of every value one instrument recorded, by its published name.</summary>
    public double SumOf(string instrumentName) =>
        _measurements.Where(measurement => measurement.Instrument == instrumentName).Sum(measurement => measurement.Value);

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double value) =>
        _measurements.Enqueue(new RecordedMeasurement(instrument.Name, value));

    private sealed record RecordedMeasurement(string Instrument, double Value);
}
