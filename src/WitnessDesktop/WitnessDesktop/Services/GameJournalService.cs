using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// In-memory game journal that tracks every chess position the brain analyzes.
/// Thread-safe via lock. Capped at 200 entries (chess games rarely exceed 100 moves).
/// </summary>
public class GameJournalService : IGameJournalService
{
    private const int MaxEntries = 200;
    private readonly List<GameJournalEntry> _entries = new();
    private readonly object _lock = new();
    private readonly ITelemetryService? _telemetry;

    public GameJournalService(ITelemetryService? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public void AddEntry(GameJournalEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);

            // Cap at MaxEntries — drop oldest when exceeded
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
        }

        _telemetry?.TrackEvent("journal", "entry_added", new Dictionary<string, string>
        {
            ["moveNumber"] = entry.MoveNumber.ToString(),
            ["hasFen"] = (entry.Fen != null).ToString()
        });
    }

    public IReadOnlyList<GameJournalEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList().AsReadOnly();
        }
    }

    public string? GetLatestFen()
    {
        lock (_lock)
        {
            return _entries.Count > 0 ? _entries[^1].Fen : null;
        }
    }

    public int EntryCount
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    public TemporalValidation ValidateTemporalConsistency(string? newFen)
    {
        if (newFen == null)
            return new TemporalValidation(true, null, "No FEN to validate");

        lock (_lock)
        {
            if (_entries.Count == 0)
                return new TemporalValidation(true, null, "First entry — no history");

            var lastEntry = _entries[^1];
            var lastFen = lastEntry.Fen;

            // Check 1: Exact duplicate FEN (same position seen twice in a row)
            if (lastFen == newFen)
                return new TemporalValidation(false, "DUPLICATE_POSITION",
                    "Exact same FEN as previous entry — possible hallucination or no move made");

            // Check 2: Piece count validation — a valid move changes at most 2 pieces (capture + promotion)
            var lastPieces = CountPieces(lastFen);
            var newPieces = CountPieces(newFen);
            if (lastPieces > 0 && newPieces > 0)
            {
                var pieceDiff = Math.Abs(lastPieces - newPieces);
                if (pieceDiff > 2)
                    return new TemporalValidation(false, "IMPOSSIBLE_TRANSITION",
                        $"Piece count changed by {pieceDiff} (max expected: 2)");
            }

            return new TemporalValidation(true, null, "Consistent");
        }
    }

    private static int CountPieces(string? fen)
    {
        if (fen == null) return -1;
        var board = fen.Split(' ')[0];
        return board.Count(c => char.IsLetter(c));
    }

    public string GetSummary()
    {
        lock (_lock)
        {
            if (_entries.Count == 0)
                return "No positions recorded yet.";

            var last = _entries[^1];
            var first = _entries[0];
            return $"Game: {_entries.Count} positions analyzed. Latest: {last.Description}. Opening FEN: {first.Fen ?? "unknown"}.";
        }
    }
}
