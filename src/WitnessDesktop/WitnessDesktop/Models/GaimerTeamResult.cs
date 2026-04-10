namespace WitnessDesktop.Models;

public record GaimerTeamResult
{
    public required string TaskId { get; init; }
    public required string Status { get; init; }
    public required string Response { get; init; }
    public List<string> ActionsTaken { get; init; } = [];
    public string? FollowUp { get; init; }
    public string? ErrorCode { get; init; }
    public List<GaimerTeamArtifact> Artifacts { get; init; } = [];
    /// <summary>Where to present: "voice", "timeline", or "both" (default). Null falls back to ResponseFormat.</summary>
    public string? Surface { get; init; }
}
