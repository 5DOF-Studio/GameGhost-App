namespace WitnessDesktop.Models;

public record GaimerTeamTask
{
    public string Id { get; init; } = "gt_" + Guid.NewGuid().ToString("N")[..12];
    public required string Task { get; init; }
    public required GaimerTeamContext Context { get; init; }
    public string ResponseFormat { get; init; } = "voice";
}
