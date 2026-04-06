namespace WitnessDesktop.Models;

public sealed record ObservationWriteRequest
{
    public required string Id { get; init; }
    public required DateTime CapturedAtUtc { get; init; }
    public required string SourceTarget { get; init; }
    public required byte[] ArtifactBytes { get; init; }
    public string? AgentKey { get; init; }
    public string? SessionId { get; init; }
    public ObservationKind Kind { get; init; } = ObservationKind.Frame;
    public string FileExtension { get; init; } = ".jpg";
}
