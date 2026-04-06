namespace WitnessDesktop.Models;

/// <summary>
/// Classification of brain pipeline output.
/// </summary>
public enum BrainResultType
{
    /// <summary>Vision model analyzed a captured game frame.</summary>
    ImageAnalysis,

    /// <summary>Tool call executed and result returned.</summary>
    ToolResult,

    /// <summary>Brain-initiated alert (blunder, opportunity, danger).</summary>
    ProactiveAlert,

    /// <summary>Error during brain processing (API failure, timeout, etc.).</summary>
    Error
}

/// <summary>
/// Delivery priority for brain results flowing through the Channel pipeline.
/// BrainEventRouter uses this to decide voice/UI routing behavior.
/// </summary>
public enum BrainResultPriority
{
    /// <summary>Interrupt voice agent immediately (blunder, critical danger).</summary>
    Interrupt,

    /// <summary>Deliver when voice agent is idle (routine analysis).</summary>
    WhenIdle,

    /// <summary>Log only, don't surface to voice or ghost mode.</summary>
    Silent
}

/// <summary>
/// The primary message type flowing through the brain Channel pipeline.
/// Produced by IBrainService, consumed by BrainEventRouter.
/// </summary>
public record BrainResult
{
    /// <summary>What kind of brain output this represents.</summary>
    public required BrainResultType Type { get; init; }

    /// <summary>Structured hint data (evaluation, suggested move, urgency). Reuses existing BrainHint model.</summary>
    public BrainHint? Hint { get; init; }

    /// <summary>Full analysis text for timeline/chat display.</summary>
    public string? AnalysisText { get; init; }

    /// <summary>Shorter narration text for voice agent to speak.</summary>
    public string? VoiceNarration { get; init; }

    /// <summary>Delivery priority. Defaults to WhenIdle for routine results.</summary>
    public BrainResultPriority Priority { get; init; } = BrainResultPriority.WhenIdle;

    /// <summary>Tracks request/response pairs. Enables stale detection when new images supersede old analyses.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>When this result was created. Defaults to current UTC time.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Tool calls executed during this brain query. Used for timeline tool-call visibility.</summary>
    public IReadOnlyList<ToolCallInfo>? ToolCalls { get; init; }

    /// <summary>Stable key for deduplicating repeated error emissions in the router.</summary>
    public string? ErrorFingerprint { get; init; }

    /// <summary>Total attempts made before this result was emitted. Relevant for retryable failures.</summary>
    public int AttemptCount { get; init; }

    /// <summary>Signals that the active session should be disconnected after routing this result.</summary>
    public bool RequestDisconnect { get; init; }

    /// <summary>Links this result to a deferred BrainRequest from voice (Phase 12D).</summary>
    public Guid? DeferredRequestId { get; init; }

    /// <summary>True when this result is a tagged answer to a deferred voice question.</summary>
    public bool IsDeferredAnswer { get; init; }
}
