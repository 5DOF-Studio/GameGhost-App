namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Simple threshold + debounce user speech detection.
/// Above threshold = speaking. Below threshold for debounce period = stopped.
/// Default threshold 0.02 RMS. Default debounce 300ms.
/// </summary>
public sealed class UserSpeechDetector : IUserSpeechDetector
{
    public const float DefaultSpeechThreshold = 0.02f;
    public const int DefaultDebounceMs = 300;

    private readonly float _threshold;
    private readonly int _debounceMs;
    private readonly object _lock = new();
    private bool _isSpeaking;
    private bool _disposed;
    private float _currentLevel;
    private Timer? _stopTimer;

    public UserSpeechDetector(float speechThreshold = DefaultSpeechThreshold, int debounceMs = DefaultDebounceMs)
    {
        _threshold = speechThreshold;
        _debounceMs = debounceMs;
    }

    public bool IsUserSpeaking { get { lock (_lock) return _isSpeaking; } }
    public float CurrentLevel { get { lock (_lock) return _currentLevel; } }
    public event EventHandler? UserSpeechStarted;
    public event EventHandler? UserSpeechStopped;

    public void OnLevelChanged(float level)
    {
        bool shouldFireStarted = false;
        lock (_lock)
        {
            if (_disposed) return;
            _currentLevel = level;
            if (level >= _threshold)
            {
                _stopTimer?.Dispose();
                _stopTimer = null;
                if (!_isSpeaking)
                {
                    _isSpeaking = true;
                    shouldFireStarted = true;
                }
            }
            else if (_isSpeaking && _stopTimer == null)
            {
                _stopTimer = new Timer(OnStopTimerFired, null,
                    TimeSpan.FromMilliseconds(_debounceMs), Timeout.InfiniteTimeSpan);
            }
        }
        if (shouldFireStarted)
            UserSpeechStarted?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _stopTimer?.Dispose();
            _stopTimer = null;
            _isSpeaking = false;
        }
    }

    private void OnStopTimerFired(object? state)
    {
        lock (_lock)
        {
            if (_disposed || !_isSpeaking) return;
            _isSpeaking = false;
            _stopTimer?.Dispose();
            _stopTimer = null;
        }
        UserSpeechStopped?.Invoke(this, EventArgs.Empty);
    }
}
