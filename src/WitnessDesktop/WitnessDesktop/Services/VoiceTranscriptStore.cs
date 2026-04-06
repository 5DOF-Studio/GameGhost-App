using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Thread-safe ring buffer of voice transcript turns with 5-minute retention.
/// Matches brain L1 retention window — anything older is out of scope for the envelope.
/// </summary>
public sealed class VoiceTranscriptStore : IVoiceTranscriptStore
{
    private readonly List<VoiceTranscriptTurn> _turns = new();
    private readonly object _lock = new();

    private static readonly TimeSpan RetentionWindow = TimeSpan.FromMinutes(5);
    private const int MaxTurns = 100;

    public void AddTurn(VoiceTranscriptTurn turn)
    {
        lock (_lock)
        {
            _turns.Add(turn);

            var cutoff = DateTime.UtcNow - RetentionWindow;
            _turns.RemoveAll(t => t.TimestampUtc < cutoff);

            while (_turns.Count > MaxTurns)
                _turns.RemoveAt(0);
        }
    }

    public IReadOnlyList<VoiceTranscriptTurn> GetRecent(int maxCount)
    {
        lock (_lock)
        {
            return _turns
                .OrderByDescending(t => t.TimestampUtc)
                .Take(maxCount)
                .ToList();
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            _turns.Clear();
        }
    }
}
