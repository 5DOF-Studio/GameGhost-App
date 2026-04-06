using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Thread-safe ring buffer of voice transcript turns.
/// 5-minute retention window matching brain L1 retention.
/// Used by BrainContextService to inject voice conversation into the brain envelope.
/// </summary>
public interface IVoiceTranscriptStore
{
    void AddTurn(VoiceTranscriptTurn turn);
    IReadOnlyList<VoiceTranscriptTurn> GetRecent(int maxCount);
    void Flush();
}
