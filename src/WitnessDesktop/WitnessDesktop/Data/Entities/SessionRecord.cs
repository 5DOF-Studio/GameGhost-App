using System.ComponentModel.DataAnnotations;

namespace WitnessDesktop.Data.Entities;

/// <summary>
/// Represents a single app session (connect → disconnect cycle).
/// SessionId reuses the 12-char hex from SessionTraceService.
/// </summary>
public class SessionRecord
{
    [Key]
    public string SessionId { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public string? AgentKey { get; set; }
    public string? GameType { get; set; }
    public string? ConnectorName { get; set; }
    public string? TargetWindowTitle { get; set; }
    public string? TargetProcessName { get; set; }
    public string? AppVersion { get; set; }

    // Navigation properties
    public ICollection<ChatMessageRecord> ChatMessages { get; set; } = new List<ChatMessageRecord>();
    public ICollection<TimelineCheckpointRecord> Checkpoints { get; set; } = new List<TimelineCheckpointRecord>();
}
