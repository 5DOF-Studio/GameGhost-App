using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public interface ISessionManager
{
    SessionContext Context { get; }
    SessionState CurrentState { get; }
    
    void TransitionToInGame(string gameId, string gameType, string connectorName);
    void TransitionToOutGame();
    
    IReadOnlyList<ToolDefinition> GetAvailableTools();
    
    event EventHandler<SessionState>? StateChanged;
}
