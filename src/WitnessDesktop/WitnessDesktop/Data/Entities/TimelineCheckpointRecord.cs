using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WitnessDesktop.Data.Entities;

/// <summary>
/// Persisted timeline checkpoint (minute-bucket container for events).
/// </summary>
public class TimelineCheckpointRecord
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? ScreenshotRef { get; set; }
    public long? GameTimeMs { get; set; }
    public string? Method { get; set; }
    public int DisplayOrder { get; set; }

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public SessionRecord Session { get; set; } = null!;

    public ICollection<TimelineEventRecord> Events { get; set; } = new List<TimelineEventRecord>();
}
