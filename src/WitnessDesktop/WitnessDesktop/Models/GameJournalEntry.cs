namespace WitnessDesktop.Models;

/// <summary>
/// A single entry in the game journal — represents one brain analysis of a chess position.
/// Tracks the position (FEN), a brief description from the brain, optional evaluation from Stockfish,
/// and a timestamp for ordering.
/// </summary>
public record GameJournalEntry(
    int MoveNumber,
    string? Fen,
    string? MoveNotation,
    string Description,
    string? Evaluation,
    DateTimeOffset Timestamp
);
