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
}
