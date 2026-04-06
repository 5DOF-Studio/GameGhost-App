namespace WitnessDesktop.Models;

/// <summary>
/// A single voice conversation turn captured from the realtime provider.
/// Stored in IVoiceTranscriptStore for brain context injection.
/// </summary>
public sealed class VoiceTranscriptTurn
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public TranscriptRole Role { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Provider { get; init; }
}

public enum TranscriptRole
{
    User,
    Assistant
}
