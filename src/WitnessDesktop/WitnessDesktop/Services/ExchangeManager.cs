using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

/// <summary>
/// Exchange state machine. Wake phrase opens, silence timer closes, connection stays warm (D-AI-1).
/// Silence timer is the sole exchange death mechanism (D-AI-3).
/// Any speech (user or agent) resets the timer.
/// Timer duration defaults to 15s (Normal). Per-pack presets added in 12E.
/// Graceful degradation: TextOnly mode makes OnWakeDetected a no-op (12E Task 3).
/// </summary>
public sealed class ExchangeManager : IExchangeManager, IDisposable
{
    public static readonly TimeSpan DefaultSilenceTimeout = TimeSpan.FromSeconds(15);

    private readonly TimeSpan _silenceTimeout;
    private readonly IGameSkillPackService? _packService;
    private readonly ITelemetryService? _telemetry;
    private readonly object _lock = new();
    private ExchangeSession? _currentExchange;
    private Timer? _silenceTimer;
    private TimeSpan _currentTimeout;
    private AudioIntelligenceMode _mode = AudioIntelligenceMode.Full;
    private bool _disposed;

    public ExchangeManager(
        TimeSpan? silenceTimeout = null,
        IGameSkillPackService? packService = null,
        ITelemetryService? telemetry = null)
    {
        _silenceTimeout = silenceTimeout ?? DefaultSilenceTimeout;
        _packService = packService;
        _telemetry = telemetry;
        _currentTimeout = _silenceTimeout;
    }

    public ExchangeState CurrentState
    {
        get { lock (_lock) return _currentExchange?.State ?? ExchangeState.Dormant; }
    }

    public ExchangeSession? CurrentExchange
    {
        get { lock (_lock) return _currentExchange; }
    }

    public bool IsExchangeActive
    {
        get
        {
            var state = CurrentState;
            return state is ExchangeState.WakeDetected
                or ExchangeState.ExchangeOpening
                or ExchangeState.ExchangeActive
                or ExchangeState.AwaitingBrain;
        }
    }

    public AudioIntelligenceMode CurrentMode => _mode;

    public void SetMode(AudioIntelligenceMode mode)
    {
        _mode = mode;
    }

    public event EventHandler<ExchangeState>? ExchangeStateChanged;
    public event EventHandler<ExchangeSession>? ExchangeOpened;
    public event EventHandler<ExchangeSession>? ExchangeClosed;

    public void OnWakeDetected(string agentName)
    {
        if (_mode == AudioIntelligenceMode.TextOnly) return; // No voice, no exchange

        _telemetry?.TrackEvent("exchange", "wake_detected", new Dictionary<string, string>
        {
            ["agent"] = agentName
        });

        ExchangeSession activeSession;
        lock (_lock)
        {
            if (_disposed) return;
            if (_currentExchange != null) return; // Already in an exchange

            // Read silence preset from active game skill pack
            _currentTimeout = _packService?.ActivePack?.SilenceTimeoutPreset switch
            {
                "Quick" => TimeSpan.FromSeconds(8),
                "Patient" => TimeSpan.FromSeconds(30),
                _ => _silenceTimeout, // "Normal" or unset — use constructor default (15s)
            };

            var now = DateTime.UtcNow;
            _currentExchange = new ExchangeSession
            {
                State = ExchangeState.WakeDetected,
                OpenedAtUtc = now,
                LastActivityUtc = now,
                AgentName = agentName,
            };
            StartSilenceTimer();
            activeSession = _currentExchange;
        }

        _telemetry?.TrackEvent("exchange", "opened", new Dictionary<string, string>
        {
            ["agent"] = agentName,
            ["silence_timeout_s"] = _currentTimeout.TotalSeconds.ToString("0"),
            ["mode"] = _mode.ToString()
        });

        // Advance through the open sequence so observers reading CurrentState during
        // callbacks see a state consistent with the event being raised.
        ExchangeStateChanged?.Invoke(this, ExchangeState.WakeDetected);
        UpdateExchangeState(ExchangeState.ExchangeOpening);
        ExchangeStateChanged?.Invoke(this, ExchangeState.ExchangeOpening);
        activeSession = UpdateExchangeState(ExchangeState.ExchangeActive);
        ExchangeStateChanged?.Invoke(this, ExchangeState.ExchangeActive);
        ExchangeOpened?.Invoke(this, activeSession);
    }

    public void OnUserSpeech()
    {
        lock (_lock)
        {
            if (_currentExchange is null || _disposed) return;
            _currentExchange = _currentExchange with { LastActivityUtc = DateTime.UtcNow };
            ResetSilenceTimer();
        }
    }

    public void OnAgentSpeech()
    {
        lock (_lock)
        {
            if (_currentExchange is null || _disposed) return;
            _currentExchange = _currentExchange with { LastActivityUtc = DateTime.UtcNow };
            ResetSilenceTimer();
        }
    }

    public void TransitionToAwaitingBrain()
    {
        lock (_lock)
        {
            if (_currentExchange is null || _disposed) return;
            if (_currentExchange.State is not (ExchangeState.ExchangeActive or ExchangeState.ExchangeOpening)) return;
            _currentExchange = _currentExchange with { State = ExchangeState.AwaitingBrain };
            ResetSilenceTimer(); // Give brain time to respond
        }
        ExchangeStateChanged?.Invoke(this, ExchangeState.AwaitingBrain);
    }

    public void CloseExchange()
    {
        ExchangeSession? closing;
        lock (_lock)
        {
            if (_currentExchange is null || _disposed) return;
            closing = _currentExchange;
            StopSilenceTimer();
            _currentExchange = null;
        }

        _telemetry?.TrackEvent("exchange", "closed", new Dictionary<string, string>
        {
            ["duration_s"] = ((DateTime.UtcNow - closing!.OpenedAtUtc).TotalSeconds).ToString("0"),
            ["agent"] = closing.AgentName ?? "unknown"
        });

        ExchangeStateChanged?.Invoke(this, ExchangeState.Dormant);
        ExchangeClosed?.Invoke(this, closing);
    }

    private void StartSilenceTimer()
    {
        StopSilenceTimer();
        _silenceTimer = new Timer(OnSilenceTimerFired, null, _currentTimeout, Timeout.InfiniteTimeSpan);
    }

    private void ResetSilenceTimer()
    {
        _silenceTimer?.Change(_currentTimeout, Timeout.InfiniteTimeSpan);
    }

    private void StopSilenceTimer()
    {
        _silenceTimer?.Dispose();
        _silenceTimer = null;
    }

    private void OnSilenceTimerFired(object? state)
    {
        CloseExchange();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            StopSilenceTimer();
            _currentExchange = null;
        }
    }

    private ExchangeSession UpdateExchangeState(ExchangeState state)
    {
        lock (_lock)
        {
            if (_currentExchange is null)
                throw new InvalidOperationException("Cannot update state when no exchange is active.");

            _currentExchange = _currentExchange with { State = state };
            return _currentExchange;
        }
    }
}
