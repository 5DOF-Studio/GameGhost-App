namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Threshold-based user speech detection from mic level events.
/// Used by VoiceDeliveryGate (12C) for D-AI-4: never barge in while user speaking.
/// </summary>
public interface IUserSpeechDetector : IDisposable
{
    bool IsUserSpeaking { get; }
    float CurrentLevel { get; }
    void OnLevelChanged(float level);
    event EventHandler? UserSpeechStarted;
    event EventHandler? UserSpeechStopped;
}
