namespace WitnessDesktop.Models;

public record GaimerTeamPermissionRequest
{
    public required string Id { get; init; }
    public required string TaskId { get; init; }
    public required string Action { get; init; }
    public required string Risk { get; init; }
    public int TimeoutSeconds { get; init; } = 60;
}
