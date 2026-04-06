namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Runtime audio intelligence capability mode, determined by connectivity state.
/// </summary>
public enum AudioIntelligenceMode
{
    /// <summary>Voice + brain + exchange state machine. Full Audio Intelligence.</summary>
    Full,

    /// <summary>Voice connected but brain unavailable. No exchange gating, voice answers directly.</summary>
    VoiceOnly,

    /// <summary>Neither voice nor brain. Chat-only fallback.</summary>
    TextOnly,
}
