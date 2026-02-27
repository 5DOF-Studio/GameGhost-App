namespace WitnessDesktop.Models;

public enum SessionState
{
    OutGame,   // No active game. Brain visual pipeline idle.
    InGame     // Game connected. Screen capture + analysis active.
}

public class SessionContext
{
    public SessionState State { get; set; } = SessionState.OutGame;
    public string? GameId { get; set; }
    public string? GameType { get; set; }
    public string? ConnectorName { get; set; }
    public DateTime? GameStartedAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserTier { get; set; } = "Adventurer";
}
