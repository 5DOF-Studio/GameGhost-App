using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public class SessionManager : ISessionManager
{
    private readonly SessionContext _context = new();
    private readonly object _lock = new();

    public SessionContext Context => _context;
    public SessionState CurrentState => _context.State;

    public event EventHandler<SessionState>? StateChanged;

    public void TransitionToInGame(string gameId, string gameType, string connectorName)
    {
        lock (_lock)
        {
            _context.State = SessionState.InGame;
            _context.GameId = gameId;
            _context.GameType = gameType;
            _context.ConnectorName = connectorName;
            _context.GameStartedAt = DateTime.UtcNow;

            StateChanged?.Invoke(this, SessionState.InGame);
        }
    }

    public void TransitionToOutGame()
    {
        lock (_lock)
        {
            _context.State = SessionState.OutGame;
            _context.GameId = null;
            _context.GameType = null;
            _context.ConnectorName = null;
            _context.GameStartedAt = null;

            StateChanged?.Invoke(this, SessionState.OutGame);
        }
    }
    
    public IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        var tools = new List<ToolDefinition>
        {
            ToolDefinitions.WebSearch,
            ToolDefinitions.SearchReplay,
        };

        if (_context.State == SessionState.InGame)
        {
            var agent = _context.AgentKey is not null ? Agents.GetByKey(_context.AgentKey) : null;
            var agentTools = agent?.Tools;

            var inGameTools = new[]
            {
                ToolDefinitions.CaptureScreen,
                ToolDefinitions.GetGameState,
                ToolDefinitions.AnalyzePositionEngine,
                ToolDefinitions.AnalyzePositionStrategic,
                ToolDefinitions.GameJournal,
            };

            foreach (var tool in inGameTools)
            {
                if (agentTools is null || agentTools.Contains(tool.Name))
                    tools.Add(tool);
            }
        }

        return tools.AsReadOnly();
    }
}
