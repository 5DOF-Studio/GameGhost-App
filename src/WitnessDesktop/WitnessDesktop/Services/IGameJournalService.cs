using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>Result of temporal consistency validation for a new journal entry.</summary>
public record TemporalValidation(bool IsConsistent, string? Warning, string Reason);

/// <summary>
/// Tracks every chess position the brain analyzes during a game session.
/// Provides a move-by-move record for context, auto new-game detection,
/// and brain tool access via game_journal.
/// </summary>
public interface IGameJournalService
{
    /// <summary>Add a new journal entry (thread-safe).</summary>
    void AddEntry(GameJournalEntry entry);

    /// <summary>Return a snapshot copy of all entries in chronological order.</summary>
    IReadOnlyList<GameJournalEntry> GetEntries();

    /// <summary>Return the FEN from the most recent entry, or null if empty.</summary>
    string? GetLatestFen();

    /// <summary>Number of entries currently tracked.</summary>
    int EntryCount { get; }

    /// <summary>Clear all entries (e.g., on new game detection).</summary>
    void Clear();

    /// <summary>Brief deterministic text summary of the game so far (~50 tokens).</summary>
    string GetSummary();

    /// <summary>
    /// Validates a FEN against the journal history for temporal consistency.
    /// Returns validation result; does NOT modify journal state.
    /// </summary>
    TemporalValidation ValidateTemporalConsistency(string? newFen);
}
