using System.Diagnostics;

namespace WitnessDesktop.Services;

/// <summary>
/// Console-based telemetry implementation that outputs structured [TEL] lines via Debug.WriteLine.
/// Thread-safe — each call is independent with no shared mutable state.
/// </summary>
public sealed class ConsoleTelemetryService : ITelemetryService
{
    /// <summary>
    /// Generates a new 8-character correlation ID for tracing pipeline flows.
    /// </summary>
    public static string NewCorrelationId() => Guid.NewGuid().ToString("N")[..8];

    /// <inheritdoc />
    public void TrackEvent(string category, string action, Dictionary<string, string>? properties = null)
    {
        var propsStr = FormatProperties(properties);
        Trace.WriteLine($"[TEL] {category}.{action}{propsStr}");
    }

    /// <inheritdoc />
    public IDisposable TrackDuration(string category, string action, Dictionary<string, string>? properties = null)
    {
        return new DurationTracker(this, category, action, properties);
    }

    private static string FormatProperties(Dictionary<string, string>? properties)
    {
        if (properties == null || properties.Count == 0)
            return string.Empty;

        var parts = new string[properties.Count];
        var i = 0;
        foreach (var kvp in properties)
        {
            parts[i++] = $"{kvp.Key}={kvp.Value}";
        }
        return " " + string.Join(" ", parts);
    }

    /// <summary>
    /// Disposable tracker that measures elapsed time and emits a [TEL] duration event on Dispose.
    /// </summary>
    private sealed class DurationTracker : IDisposable
    {
        private readonly ConsoleTelemetryService _telemetry;
        private readonly string _category;
        private readonly string _action;
        private readonly Dictionary<string, string>? _properties;
        private readonly Stopwatch _stopwatch;
        private int _disposed;

        public DurationTracker(
            ConsoleTelemetryService telemetry,
            string category,
            string action,
            Dictionary<string, string>? properties)
        {
            _telemetry = telemetry;
            _category = category;
            _action = action;
            _properties = properties;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return; // Already disposed — idempotent

            _stopwatch.Stop();
            var merged = new Dictionary<string, string>(_properties ?? new())
            {
                ["duration_ms"] = _stopwatch.ElapsedMilliseconds.ToString()
            };
            _telemetry.TrackEvent(_category, _action, merged);
        }
    }
}
