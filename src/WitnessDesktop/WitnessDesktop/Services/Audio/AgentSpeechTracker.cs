using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Gap-based agent speech detection. Audio arriving = speaking.
/// Silence gap > threshold (default 500ms) = stopped.
/// </summary>
public sealed class AgentSpeechTracker : IAgentSpeechTracker
{
    public static readonly TimeSpan DefaultSilenceGap = TimeSpan.FromMilliseconds(500);

    private readonly TimeSpan _silenceGap;
    private readonly object _lock = new();
    private readonly ISessionTraceService? _sessionTrace;
    private Timer? _gapTimer;
    private bool _isSpeaking;
    private bool _disposed;
    private DateTimeOffset _speechStartedAt;

    public AgentSpeechTracker(TimeSpan? silenceGap = null, ISessionTraceService? sessionTrace = null)
    {
        _silenceGap = silenceGap ?? DefaultSilenceGap;
        _sessionTrace = sessionTrace;
    }

    public bool IsSpeaking { get { lock (_lock) return _isSpeaking; } }
    public event EventHandler? SpeakingStarted;
    public event EventHandler? SpeakingStopped;

    public void OnAudioReceived()
    {
        bool wasNotSpeaking;
        lock (_lock)
        {
            if (_disposed) return;
            wasNotSpeaking = !_isSpeaking;
            _isSpeaking = true;
            if (wasNotSpeaking)
                _speechStartedAt = DateTimeOffset.UtcNow;
            ResetGapTimer();
        }
        if (wasNotSpeaking)
        {
            _sessionTrace?.TrackEvent("audio.agent_speech.started");
            SpeakingStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Reset()
    {
        bool wasSpeaking;
        lock (_lock)
        {
            wasSpeaking = _isSpeaking;
            _isSpeaking = false;
            StopGapTimer();
        }
        if (wasSpeaking)
            SpeakingStopped?.Invoke(this, EventArgs.Empty);
    }

    private void ResetGapTimer()
    {
        _gapTimer?.Dispose();
        _gapTimer = new Timer(OnGapTimerFired, null, _silenceGap, Timeout.InfiniteTimeSpan);
    }

    private void StopGapTimer()
    {
        _gapTimer?.Dispose();
        _gapTimer = null;
    }

    private void OnGapTimerFired(object? state)
    {
        DateTimeOffset startedAt;
        lock (_lock)
        {
            if (_disposed || !_isSpeaking) return;
            startedAt = _speechStartedAt;
            _isSpeaking = false;
            StopGapTimer();
        }
        var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        _sessionTrace?.TrackEvent("audio.agent_speech.stopped", new Dictionary<string, string>
        {
            ["duration_ms"] = durationMs.ToString()
        });
        SpeakingStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            StopGapTimer();
            _isSpeaking = false;
        }
    }
}
