namespace WitnessDesktop.Services;

/// <summary>
/// Lightweight telemetry abstraction for structured pipeline observability.
/// All pipeline events emit structured telemetry with correlation IDs
/// to enable end-to-end trace of capture -> brain -> tool -> route flows.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Fires a structured one-liner telemetry event.
    /// Output format: [TEL] {category}.{action} {key=value key=value}
    /// </summary>
    /// <param name="category">Event category (e.g., "brain", "tool", "router", "capture").</param>
    /// <param name="action">Event action (e.g., "submit_image", "called", "result_routed").</param>
    /// <param name="properties">Optional key-value properties to include in the event.</param>
    void TrackEvent(string category, string action, Dictionary<string, string>? properties = null);

    /// <summary>
    /// Returns a disposable that logs duration on Dispose.
    /// Output format: [TEL] {category}.{action} duration_ms={elapsed} {key=value}
    /// </summary>
    /// <param name="category">Event category.</param>
    /// <param name="action">Event action.</param>
    /// <param name="properties">Optional key-value properties to include in the event.</param>
    /// <returns>An IDisposable that emits the duration event when disposed.</returns>
    IDisposable TrackDuration(string category, string action, Dictionary<string, string>? properties = null);
}
