namespace WitnessDesktop.Models;

public record GaimerTeamContext
{
    public required string Game { get; init; }
    public required string Agent { get; init; }
    public required string SessionId { get; init; }
    public string? RecentActivity { get; init; }
    public string? L1Context { get; init; }
    public string? L2Context { get; init; }
}
