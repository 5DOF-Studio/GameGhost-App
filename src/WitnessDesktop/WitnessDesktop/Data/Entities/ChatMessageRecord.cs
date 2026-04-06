using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WitnessDesktop.Data.Entities;

/// <summary>
/// Persisted chat message (user, assistant, system, proactive).
/// Role and Intent stored as enum name strings for readability.
/// </summary>
public class ChatMessageRecord
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }

    /// <summary>MessageRole enum name (User, Assistant, System, Proactive).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>MessageIntent enum name, nullable for system/internal messages.</summary>
    public string? Intent { get; set; }

    public string Content { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? DeliveryState { get; set; }
    public string? CorrelationId { get; set; }

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public SessionRecord Session { get; set; } = null!;
}
