namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Tracks when the AI agent is producing audio output.
/// Used by VoiceDeliveryGate (12C) for D-AI-4 barge-in gating.
/// </summary>
public interface IAgentSpeechTracker : IDisposable
{
    bool IsSpeaking { get; }
    void OnAudioReceived();
    void Reset();
    event EventHandler? SpeakingStarted;
    event EventHandler? SpeakingStopped;
}
