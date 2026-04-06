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
    private Timer? _gapTimer;
    private bool _isSpeaking;
    private bool _disposed;

    public AgentSpeechTracker(TimeSpan? silenceGap = null)
    {
        _silenceGap = silenceGap ?? DefaultSilenceGap;
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
            ResetGapTimer();
        }
        if (wasNotSpeaking)
            SpeakingStarted?.Invoke(this, EventArgs.Empty);
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
        lock (_lock)
        {
            if (_disposed || !_isSpeaking) return;
            _isSpeaking = false;
            StopGapTimer();
        }
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
