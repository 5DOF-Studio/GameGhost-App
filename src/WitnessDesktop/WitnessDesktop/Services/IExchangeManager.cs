using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

/// <summary>
/// Manages exchange lifecycle: wake → active → silence timeout → dormant.
/// Connection stays warm across exchange boundaries (D-AI-1).
/// Silence timer is exchange death timer only (D-AI-3).
/// </summary>
public interface IExchangeManager
{
    /// <summary>Current exchange state. Dormant when no exchange is active.</summary>
    ExchangeState CurrentState { get; }

    /// <summary>Current exchange session, or null when Dormant.</summary>
    ExchangeSession? CurrentExchange { get; }

    /// <summary>True when state is WakeDetected, ExchangeOpening, ExchangeActive, or AwaitingBrain.</summary>
    bool IsExchangeActive { get; }

    /// <summary>Current audio intelligence mode. Determines degradation behavior.</summary>
    AudioIntelligenceMode CurrentMode { get; }

    /// <summary>Set the audio intelligence mode based on connectivity state.</summary>
    void SetMode(AudioIntelligenceMode mode);

    /// <summary>Called when wake phrase detected. Transitions Dormant → ExchangeActive.</summary>
    void OnWakeDetected(string agentName);

    /// <summary>Called on user speech. Resets silence timer (D-AI-3).</summary>
    void OnUserSpeech();

    /// <summary>Called on agent speech. Resets silence timer (D-AI-3).</summary>
    void OnAgentSpeech();

    /// <summary>Manually close the current exchange.</summary>
    void CloseExchange();

    /// <summary>Transition to AwaitingBrain state (voice deferred to brain).</summary>
    void TransitionToAwaitingBrain();

    /// <summary>Fired on every state transition.</summary>
    event EventHandler<ExchangeState>? ExchangeStateChanged;

    /// <summary>Fired when an exchange opens (WakeDetected → ExchangeActive).</summary>
    event EventHandler<ExchangeSession>? ExchangeOpened;

    /// <summary>Fired when an exchange closes (any active state → Dormant).</summary>
    event EventHandler<ExchangeSession>? ExchangeClosed;
}
