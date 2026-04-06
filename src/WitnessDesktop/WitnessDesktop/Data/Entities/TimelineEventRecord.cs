using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WitnessDesktop.Data.Entities;

/// <summary>
/// Persisted timeline event. CheckpointId is nullable for events
/// that exist outside checkpoint context (e.g. direct messages).
/// </summary>
public class TimelineEventRecord
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;
    public string? CheckpointId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>EventOutputType enum name (Danger, Assessment, SageAdvice, DirectMessage, etc.).</summary>
    public string Type { get; set; } = string.Empty;

    public string? Role { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? FullContent { get; set; }
    public string? Icon { get; set; }
    public string? CapsuleColorHex { get; set; }
    public string? CapsuleStrokeHex { get; set; }
    public string? LinkedMessageId { get; set; }

    // Brain metadata fields (flattened from BrainMetadata)
    public string? BrainSignal { get; set; }
    public string? BrainUrgency { get; set; }
    public int? BrainEvaluation { get; set; }
    public int? BrainEvalDelta { get; set; }
    public string? SuggestedAction { get; set; }

    // Tool call fields
    public string? ToolName { get; set; }
    public int? ToolDurationMs { get; set; }

    public int DisplayOrder { get; set; }

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public SessionRecord Session { get; set; } = null!;

    [ForeignKey(nameof(CheckpointId))]
    public TimelineCheckpointRecord? Checkpoint { get; set; }
}
