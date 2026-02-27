namespace WitnessDesktop.Models.Timeline;

public class TimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public EventOutputType Type { get; set; }
    public string? Icon { get; set; }
    public string? CapsuleColorHex { get; set; }
    public string? CapsuleStrokeHex { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? FullContent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BrainMetadata? Brain { get; set; }
    public MessageRole? Role { get; set; }
    public ChatMessage? LinkedMessage { get; set; }

    /// <summary>Resolved capsule background color for XAML binding.</summary>
    public Color CapsuleColor => Color.FromArgb(CapsuleColorHex ?? "#30808080");

    /// <summary>Resolved capsule stroke color for XAML binding.</summary>
    public Color CapsuleStroke => Color.FromArgb(CapsuleStrokeHex ?? "#50808080");
}
