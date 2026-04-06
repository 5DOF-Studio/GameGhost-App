namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Detects wake phrases in transcript text.
/// MVP: regex matching on "Hey {AgentName}".
/// Upgrade path: Porcupine on-device wake word engine (D-AI-7).
/// </summary>
public interface IWakePhraseDetector
{
    /// <summary>
    /// Checks if the transcript contains a wake phrase for the given agent.
    /// </summary>
    bool TryDetectWake(string transcript, string agentName, out string? matchedPhrase);
}
