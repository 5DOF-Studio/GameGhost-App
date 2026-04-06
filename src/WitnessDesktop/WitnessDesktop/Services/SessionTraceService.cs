using System.Text.Json;

namespace WitnessDesktop.Services;

public sealed class SessionTraceService : ISessionTraceService
{
    private readonly string _traceDirectory;
    private readonly object _lock = new();
    private StreamWriter? _writer;

    public string? RunId { get; private set; }
    public string? SessionId { get; private set; }

    public SessionTraceService(string? traceDirectory = null)
    {
        _traceDirectory = traceDirectory
            ?? Path.Combine(Path.GetTempPath(), "gaimer-traces");
    }

    public void StartRun()
    {
        RunId = Guid.NewGuid().ToString("N")[..12];

        Directory.CreateDirectory(_traceDirectory);

        var fileName = $"trace-{RunId}.jsonl";
        var filePath = Path.Combine(_traceDirectory, fileName);
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = false };
        Console.WriteLine($"[SessionTraceService] Writing session trace to {filePath}");

        WriteEvent("app.bootstrap");
    }

    public void EndRun()
    {
        WriteEvent("app.shutdown");
        Flush();
    }

    public void StartSession()
    {
        SessionId = Guid.NewGuid().ToString("N")[..12];
    }

    public void EndSession()
    {
        WriteEvent("session.disconnect");
        SessionId = null;
    }

    public void TrackEvent(string eventName, Dictionary<string, string>? payload = null)
    {
        WriteEvent(eventName, payload);
    }

    public void TrackError(string message, string source, Dictionary<string, string>? extra = null)
    {
        var payload = new Dictionary<string, string>
        {
            ["message"] = message,
            ["source"] = source
        };
        if (extra != null)
        {
            foreach (var kvp in extra)
                payload[kvp.Key] = kvp.Value;
        }
        WriteEvent("error", payload);
    }

    public void TrackSessionResult(bool success, string? error = null)
    {
        var eventName = success ? "session.connect.success" : "session.connect.failure";
        Dictionary<string, string>? payload = null;
        if (error != null)
        {
            payload = new Dictionary<string, string> { ["error"] = error };
        }
        WriteEvent(eventName, payload);
    }

    public void Flush()
    {
        lock (_lock)
        {
            _writer?.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void WriteEvent(string eventName, Dictionary<string, string>? payload = null)
    {
        var traceEvent = new TraceEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            RunId = RunId,
            SessionId = SessionId,
            EventName = eventName,
            Payload = payload
        };

        var json = JsonSerializer.Serialize(traceEvent, TraceEvent.JsonOptions);

        lock (_lock)
        {
            _writer?.WriteLine(json);
        }
    }
}
