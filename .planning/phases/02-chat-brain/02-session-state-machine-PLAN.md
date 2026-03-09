# Plan 02: Session State Machine

**Phase:** 02-chat-brain  
**Status:** ✅ Complete  
**Estimated Effort:** 2-3 hours  
**Actual Effort:** ~30 min  
**Dependencies:** 01-core-models  
**Provides:** Session state transitions, tool availability gating

---

## Objective

Implement the session state machine that tracks OutGame/InGame state and controls tool availability for the LLM.

---

## Tasks

### 1. Session Manager Service

**File:** `src/GaimerDesktop/GaimerDesktop/Services/ISessionManager.cs`

```csharp
public interface ISessionManager
{
    SessionContext Context { get; }
    SessionState CurrentState { get; }
    
    void TransitionToInGame(string gameId, string gameType, string connectorName);
    void TransitionToOutGame();
    
    IReadOnlyList<ToolDefinition> GetAvailableTools();
    
    event EventHandler<SessionState>? StateChanged;
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Services/SessionManager.cs`

```csharp
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
            // Always available
            ToolDefinitions.WebSearch,
            ToolDefinitions.PlayerHistory,
            ToolDefinitions.PlayerAnalytics,
        };
        
        if (_context.State == SessionState.InGame)
        {
            // In-game only — Brain's live tools
            tools.Add(ToolDefinitions.CaptureScreen);
            tools.Add(ToolDefinitions.GetBestMove);
            tools.Add(ToolDefinitions.GetGameState);
            tools.Add(ToolDefinitions.HighlightRegion);
            tools.Add(ToolDefinitions.PlayMusic);
        }
        
        return tools.AsReadOnly();
    }
}
```

### 2. Tool Definitions

**File:** `src/GaimerDesktop/GaimerDesktop/Models/ToolDefinition.cs`

```csharp
public class ToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ParametersSchema { get; init; } = "{}";
    public bool RequiresInGame { get; init; }
}

public static class ToolDefinitions
{
    // Always available
    public static readonly ToolDefinition WebSearch = new()
    {
        Name = "web_search",
        Description = "Search the web for game guides, wiki information, or strategy tips.",
        RequiresInGame = false
    };
    
    public static readonly ToolDefinition PlayerHistory = new()
    {
        Name = "player_history",
        Description = "Retrieve past gaming sessions and match history.",
        RequiresInGame = false
    };
    
    public static readonly ToolDefinition PlayerAnalytics = new()
    {
        Name = "player_analytics",
        Description = "Get win rates, performance trends, and statistical analysis.",
        RequiresInGame = false
    };
    
    // In-game only
    public static readonly ToolDefinition CaptureScreen = new()
    {
        Name = "capture_screen",
        Description = "Capture the current game screen for analysis.",
        RequiresInGame = true
    };
    
    public static readonly ToolDefinition GetBestMove = new()
    {
        Name = "get_best_move",
        Description = "Get the Brain's recommended next action based on current position.",
        RequiresInGame = true
    };
    
    public static readonly ToolDefinition GetGameState = new()
    {
        Name = "get_game_state",
        Description = "Get the current game state including position, score, and recent actions.",
        RequiresInGame = true
    };
    
    public static readonly ToolDefinition HighlightRegion = new()
    {
        Name = "highlight_region",
        Description = "Highlight a specific area or element on the game screen.",
        RequiresInGame = true
    };
    
    public static readonly ToolDefinition PlayMusic = new()
    {
        Name = "play_music",
        Description = "Control background music playback.",
        RequiresInGame = true
    };
}
```

### 3. Integration with MainViewModel

Update `MainViewModel` to use `ISessionManager` for state transitions:

```csharp
// In ConnectToGame:
_sessionManager.TransitionToInGame(
    gameId: CurrentTarget?.ProcessId.ToString() ?? "unknown",
    gameType: CurrentTarget?.Title?.Contains("chess", StringComparison.OrdinalIgnoreCase) == true ? "chess" : "general",
    connectorName: CurrentTarget?.Title ?? "Unknown"
);

// In Disconnect:
_sessionManager.TransitionToOutGame();
```

### 4. DI Registration

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

```csharp
builder.Services.AddSingleton<ISessionManager, SessionManager>();
```

---

## Verification

- [x] `SessionManager` correctly transitions between OutGame and InGame
- [x] `GetAvailableTools()` returns only always-available tools when OutGame
- [x] `GetAvailableTools()` includes in-game tools when InGame
- [x] `StateChanged` event fires on transitions
- [x] `MainViewModel` connection/disconnection updates session state

### Implementation Notes (2026-02-24)

- Created `ISessionManager.cs` interface and `SessionManager.cs` implementation
- Created `ToolDefinition.cs` with 8 tool definitions (3 always-available, 5 in-game only)
- Registered `ISessionManager` as singleton in `MauiProgram.cs`
- Injected `ISessionManager` into `MainViewModel` constructor
- Added `TransitionToInGame()` call after successful provider connection
- Added `TransitionToOutGame()` call at start of `StopSessionAsync()`
- Game type detection uses window title for chess, agent type for others

---

## State Diagram

```
┌─────────────┐     Connect to game      ┌─────────────┐
│  OUT-GAME   │ ──────────────────────►  │   IN-GAME   │
│             │                           │             │
│ Tools:      │    Disconnect / Game Over │ Tools:      │
│ - WebSearch │ ◄────────────────────────  │ - WebSearch │
│ - History   │                           │ - History   │
│ - Analytics │                           │ - Analytics │
│             │                           │ + Capture   │
│ Brain idle  │                           │ + BestMove  │
└─────────────┘                           │ + GameState │
                                          │ + Highlight │
                                          │ + Music     │
                                          │             │
                                          │ Brain active│
                                          └─────────────┘
```

---

## Files to Create

```
src/GaimerDesktop/GaimerDesktop/Services/ISessionManager.cs
src/GaimerDesktop/GaimerDesktop/Services/SessionManager.cs
src/GaimerDesktop/GaimerDesktop/Models/ToolDefinition.cs
```

## Files to Modify

```
src/GaimerDesktop/GaimerDesktop/MauiProgram.cs
src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs
```
