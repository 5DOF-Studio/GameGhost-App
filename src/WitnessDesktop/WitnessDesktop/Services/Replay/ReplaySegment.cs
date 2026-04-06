namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Represents a completed recording segment on disk.
/// </summary>
public record ReplaySegment
{
    public required string FilePath { get; init; }
    public required string SessionId { get; init; }
    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset EndUtc { get; init; }
    public long ByteSize { get; init; }
    public TimeSpan Duration => EndUtc - StartUtc;
    public int SegmentIndex { get; init; }
}

/// <summary>
/// Event args for when a segment file is finalized and safe to read.
/// </summary>
public class ReplaySegmentCompletedEventArgs : EventArgs
{
    public required ReplaySegment Segment { get; init; }
}
