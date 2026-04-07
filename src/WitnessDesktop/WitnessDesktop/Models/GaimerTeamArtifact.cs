namespace WitnessDesktop.Models;

public record GaimerTeamArtifact
{
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
}
