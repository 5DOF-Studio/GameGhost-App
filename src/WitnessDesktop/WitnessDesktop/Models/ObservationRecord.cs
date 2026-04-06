namespace WitnessDesktop.Models;

public sealed record ObservationRecord
{
    public required string Id { get; init; }
    public ObservationKind Kind { get; init; } = ObservationKind.Frame;
    public DateTime CapturedAtUtc { get; init; }
    public string SourceTarget { get; init; } = string.Empty;
    public string? AgentKey { get; init; }
    public string? SessionId { get; init; }
    public string ArtifactPath { get; init; } = string.Empty;
    public long ByteSize { get; init; }
}
