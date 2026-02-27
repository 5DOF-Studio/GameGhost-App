namespace WitnessDesktop.Models;

/// <summary>
/// Represents a single event in the brain's L1 immediate window.
/// Aligned with BRAIN_CONTEXT_PIPELINE_SPEC BrainEvent contract.
/// </summary>
public sealed class BrainEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; init; }
    public BrainEventType Type { get; init; }
    public string Category { get; init; } = string.Empty; // e.g. threat, objective, economy
    public string Text { get; init; } = string.Empty;
    public double Confidence { get; init; } // 0..1
    public TimeSpan? ValidFor { get; init; } // optional staleness window
    public Dictionary<string, string>? Tags { get; init; } // map, mode, team, etc.
}

/// <summary>
/// Event type classification for the brain pipeline.
/// </summary>
public enum BrainEventType
{
    VisionObservation,
    GameplayState,
    UserAction,
    SystemSignal
}
