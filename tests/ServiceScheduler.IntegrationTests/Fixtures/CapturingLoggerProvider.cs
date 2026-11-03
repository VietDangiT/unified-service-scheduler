using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace ServiceScheduler.IntegrationTests.Fixtures;

// Records every entry the host's logging filters let through, from every category, so a test can read entries by
// their EventId name, level and structured values. Requests run on pool threads, hence the concurrent collection.
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyList<CapturedLogEntry> Entries => [.. _entries];

    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    // The host never owns this instance: the test that created it reads it after the host is gone.
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            IReadOnlyList<KeyValuePair<string, object?>> values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? [.. pairs]
                : [];

            entries.Enqueue(new CapturedLogEntry(category, logLevel, eventId, formatter(state, exception), values, exception));
        }
    }
}

internal sealed record CapturedLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> Values,
    Exception? Exception)
{
    public object? ValueOf(string key) => Values.SingleOrDefault(pair => pair.Key == key).Value;

    public bool Contains(string text) =>
        Message.Contains(text, StringComparison.OrdinalIgnoreCase)
        || Values.Any(pair => Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture)?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
}
