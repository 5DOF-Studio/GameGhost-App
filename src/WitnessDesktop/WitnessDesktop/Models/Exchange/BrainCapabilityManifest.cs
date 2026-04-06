namespace WitnessDesktop.Models.Exchange;

/// <summary>
/// Lightweight list of brain capabilities for voice-side awareness.
/// Voice doesn't call tools — but needs to know what brain CAN do
/// to give appropriate deferral acknowledgments (spec Section 23.1).
/// </summary>
public sealed class BrainCapabilityManifest
{
    public static readonly BrainCapabilityManifest Default = new()
    {
        Capabilities = new[]
        {
            new BrainCapability
            {
                Name = "search_replay",
                Description = "Search recent gameplay footage for specific events",
                TriggerPhrases = new[] { "what happened", "how did i die", "earlier", "last round", "show me", "that play" }
            },
            new BrainCapability
            {
                Name = "analyze_position_engine",
                Description = "Run Stockfish chess engine analysis on current position",
                TriggerPhrases = new[] { "run the engine", "best move", "analyze this", "stockfish", "engine says" }
            },
            new BrainCapability
            {
                Name = "game_journal",
                Description = "Read or update the game journal",
                TriggerPhrases = new[] { "check my journal", "what did i note", "journal entry" }
            },
        }
    };

    public IReadOnlyList<BrainCapability> Capabilities { get; init; } = Array.Empty<BrainCapability>();

    /// <summary>Find the most likely capability for a user question, or null.</summary>
    public BrainCapability? FindMatchingCapability(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return null;
        var lower = userText.ToLowerInvariant();
        return Capabilities.FirstOrDefault(c =>
            c.TriggerPhrases.Any(p => lower.Contains(p)));
    }
}

public sealed class BrainCapability
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] TriggerPhrases { get; init; }
}
