namespace WitnessDesktop.Models;

/// <summary>
/// Classification of a voice turn's factual requirements.
/// </summary>
public enum VoiceTurnClass
{
    /// <summary>Requires grounded board-state knowledge (e.g., "what's on e4?", "am I winning?").</summary>
    BoardSensitive,

    /// <summary>General game question answerable without live board state (e.g., "what's a Sicilian Defense?").</summary>
    GeneralGameQuestion,

    /// <summary>Social/conversational turn (e.g., "that's crazy", "thanks").</summary>
    Social,

    /// <summary>Control instruction (e.g., "be quieter", "stop talking").</summary>
    Control,

    /// <summary>Cannot classify confidently.</summary>
    Unclear,

    /// <summary>Requires past gameplay data (e.g., "what happened earlier", "how did I die").</summary>
    HistorySensitive,

    /// <summary>Requires a brain tool voice doesn't have (e.g., "run the engine", "search replay").</summary>
    ToolDependent,
}

/// <summary>
/// How voice should respond given grounding state.
/// </summary>
public enum VoiceResponseMode
{
    /// <summary>Safe to answer without fresh board facts.</summary>
    AnswerDirectly,

    /// <summary>Answer using recent grounded facts.</summary>
    RespondFromGroundedContext,

    /// <summary>Acknowledge and indicate checking the board.</summary>
    AcknowledgeAndRefresh,

    /// <summary>Explicitly express uncertainty about board state.</summary>
    AcknowledgeUncertainty,

    /// <summary>Do not make board-state claims.</summary>
    DeclineBoardCertainty,

    /// <summary>Defer to brain — voice cannot answer, gives acknowledgment and writes BrainRequest.</summary>
    DeferToBrain,
}

/// <summary>
/// Decision produced by the voice grounding coordinator for a given turn.
/// </summary>
public sealed record VoiceGroundingDecision(
    VoiceTurnClass TurnClass,
    VoiceResponseMode ResponseMode,
    bool HasFreshGroundedContext,
    string? GroundedSummary,
    string? Reason);

/// <summary>
/// Compact factual envelope produced from brain results for voice grounding.
/// App-owned structured data, not free-form provider memory.
/// </summary>
public sealed class GroundedVoiceContext
{
    public string? PositionAssessment { get; init; }
    public string? Threats { get; init; }
    public string? SuggestedAction { get; init; }
    public string? Fen { get; init; }
    public string? Confidence { get; init; }

    /// <summary>Generic observations from pack-driven routing. Keys are field names, values are string representations.</summary>
    public Dictionary<string, string>? Observations { get; init; }

    public DateTime CapturedAtUtc { get; init; }

    /// <summary>
    /// Whether this context is considered stale for voice grounding purposes.
    /// </summary>
    public bool IsStale(DateTime nowUtc, TimeSpan maxAge)
        => (nowUtc - CapturedAtUtc) > maxAge;

    /// <summary>
    /// Produces a concise summary suitable for voice grounding injection.
    /// </summary>
    public string ToGroundingSummary()
    {
        // Pack-driven: build summary from generic observations
        if (Observations is { Count: > 0 })
        {
            var parts = new List<string>();
            foreach (var (key, value) in Observations)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add(value);
            }
            return parts.Count > 0 ? string.Join(". ", parts) : string.Empty;
        }

        // Legacy: chess-specific typed properties
        var legacyParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(PositionAssessment))
            legacyParts.Add(PositionAssessment);
        if (!string.IsNullOrWhiteSpace(Threats))
            legacyParts.Add($"Threats: {Threats}");
        if (!string.IsNullOrWhiteSpace(SuggestedAction))
            legacyParts.Add($"Suggestion: {SuggestedAction}");
        return legacyParts.Count > 0 ? string.Join(". ", legacyParts) : string.Empty;
    }
}
