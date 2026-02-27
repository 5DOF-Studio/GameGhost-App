namespace WitnessDesktop.Models;

/// <summary>
/// Represents a single captured frame moment in the visual reel.
/// Aligned with BRAIN_CONTEXT_PIPELINE_SPEC ReelMoment contract.
/// </summary>
public sealed class ReelMoment
{
    /// <summary>
    /// Unique identifier for this moment.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// UTC timestamp when the frame was captured.
    /// </summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Source target identifier (e.g. process|window title).
    /// </summary>
    public string SourceTarget { get; init; } = string.Empty;

    /// <summary>
    /// Local pointer/path/key for the frame (in-memory MVP: moment Id or storage key).
    /// </summary>
    public string? FrameRef { get; init; }

    /// <summary>
    /// Optional link to BrainEvent.Id when moment is tied to an extracted event.
    /// </summary>
    public string? EventRef { get; init; }

    /// <summary>
    /// Confidence score 0..1 (extraction confidence if generated, or 1.0 for raw capture).
    /// </summary>
    public double Confidence { get; init; } = 1.0;
}
