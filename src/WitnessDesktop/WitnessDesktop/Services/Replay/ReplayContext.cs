namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Time-windowed replay context assembling multimodal items from
/// the history DB and observation store into chronological order.
/// </summary>
public sealed class ReplayContext
{
    public string SessionId { get; init; } = string.Empty;
    public DateTime WindowStartUtc { get; init; }
    public DateTime WindowEndUtc { get; init; }
    public IReadOnlyList<ReplayItem> Items { get; init; } = Array.Empty<ReplayItem>();
}

/// <summary>
/// A single item in a replay context: chat message, timeline event, or capture artifact.
/// </summary>
public sealed class ReplayItem
{
    public DateTime TimestampUtc { get; init; }
    public ReplayItemKind Kind { get; init; }

    // Chat message fields
    public string? MessageContent { get; init; }
    public string? MessageRole { get; init; }

    // Timeline event fields
    public string? EventSummary { get; init; }
    public string? EventType { get; init; }

    // Capture artifact fields
    public string? ArtifactPath { get; init; }
    public bool ArtifactExists { get; init; }

    // Brain metadata fields (for timeline events)
    public string? BrainSignal { get; init; }
    public string? SuggestedAction { get; init; }
}

/// <summary>
/// Kind of replay item.
/// </summary>
public enum ReplayItemKind
{
    ChatMessage,
    TimelineEvent,
    CaptureArtifact
}
