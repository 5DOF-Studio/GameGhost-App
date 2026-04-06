namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Exchange lifecycle states (spec Section 5.1 — all 8 states).
/// An exchange is a bounded interval where the user explicitly addresses the agent.
/// </summary>
public enum ExchangeState
{
    /// <summary>No directed interaction in progress. Wake detection active.</summary>
    Dormant,

    /// <summary>Wake phrase detected. Preparing to open exchange.</summary>
    WakeDetected,

    /// <summary>Exchange acknowledged. Short acknowledgment window.</summary>
    ExchangeOpening,

    /// <summary>User actively addressing agent. Direct voice responses allowed.</summary>
    ExchangeActive,

    /// <summary>Exchange active but voice deferred to brain. Awaiting tagged response.</summary>
    AwaitingBrain,

    /// <summary>Fresh result arrived after exchange expired. Queued for next exchange.</summary>
    ReminderQueued,

    /// <summary>Final spoken turn / graceful close in progress.</summary>
    ExchangeClosing,

    /// <summary>Exchange closed. No voice injection unless barge-in allows it.</summary>
    ExchangeExpired,
}
