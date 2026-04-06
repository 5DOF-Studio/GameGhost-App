namespace WitnessDesktop.Services;

public interface ISessionTraceService : IDisposable
{
    string? RunId { get; }
    string? SessionId { get; }

    void StartRun();
    void EndRun();

    void StartSession();
    void EndSession();

    void TrackEvent(string eventName, Dictionary<string, string>? payload = null);
    void TrackError(string message, string source, Dictionary<string, string>? extra = null);
    void TrackSessionResult(bool success, string? error = null);

    void Flush();
}
