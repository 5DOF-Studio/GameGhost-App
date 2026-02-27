using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public class SessionManager : ISessionManager
{
    private readonly SessionContext _context = new();
    
    public SessionContext Context => _context;
    public SessionState CurrentState => _context.State;
    
    public event EventHandler<SessionState>? StateChanged;
    
    public void TransitionToInGame(string gameId, string gameType, string connectorName)
    {
        _context.State = SessionState.InGame;
        _context.GameId = gameId;
        _context.GameType = gameType;
        _context.ConnectorName = connectorName;
        _context.GameStartedAt = DateTime.UtcNow;
        
        StateChanged?.Invoke(this, SessionState.InGame);
    }
    
    public void TransitionToOutGame()
    {
        _context.State = SessionState.OutGame;
        _context.GameId = null;
        _context.GameType = null;
        _context.ConnectorName = null;
        _context.GameStartedAt = null;
        
        StateChanged?.Invoke(this, SessionState.OutGame);
    }
    
    public IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        var tools = new List<ToolDefinition>
        {
            ToolDefinitions.WebSearch,
            ToolDefinitions.PlayerHistory,
            ToolDefinitions.PlayerAnalytics,
        };
        
        if (_context.State == SessionState.InGame)
        {
            tools.Add(ToolDefinitions.CaptureScreen);
            tools.Add(ToolDefinitions.GetBestMove);
            tools.Add(ToolDefinitions.GetGameState);
            tools.Add(ToolDefinitions.HighlightRegion);
            tools.Add(ToolDefinitions.PlayMusic);
        }
        
        return tools.AsReadOnly();
    }
}
