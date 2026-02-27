using System.Collections.Immutable;

namespace WitnessDesktop.Models;

/// <summary>
/// Budgeted context packet for voice/chat pipelines.
/// Aligned with BRAIN_CONTEXT_PIPELINE_SPEC SharedContextEnvelope contract.
/// </summary>
public sealed class SharedContextEnvelope
{
    public DateTime RequestTsUtc { get; init; }
    public string Intent { get; init; } = "general"; // tactical, explanation, recap
    public int BudgetTokens { get; init; }

    public IReadOnlyList<BrainEvent> ImmediateEvents { get; init; } = ImmutableArray<BrainEvent>.Empty;
    public string RollingSummary { get; init; } = string.Empty;
    public string? SessionNarrative { get; init; }

    public IReadOnlyList<ReelMoment> ReelRefs { get; init; } = ImmutableArray<ReelMoment>.Empty;
    public string TruncationReport { get; init; } = string.Empty;
    public double EnvelopeConfidence { get; init; } // 0..1

    /// <summary>
    /// Active target/game metadata (process, window).
    /// </summary>
    public string ActiveTargetMetadata { get; init; } = string.Empty;

    /// <summary>
    /// Recent chat turns included in budget (for interim prefixed-text path).
    /// </summary>
    public string RecentChatSummary { get; init; } = string.Empty;
}
