namespace WitnessDesktop.Models;

public class ToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "tool_generic.png";
    public string ActionLabel { get; init; } = string.Empty;
    public string ParametersSchema { get; init; } = "{}";
    public bool RequiresInGame { get; init; }
}

public static class ToolDefinitions
{
    public static IReadOnlyList<ToolDefinition> All =>
    [
        WebSearch,
        CaptureScreen,
        GetGameState,
        AnalyzePositionEngine,
        AnalyzePositionStrategic,
        GameJournal,
        SearchReplay,
        ShowReplay,
        DelegateToTeam
    ];

    public static ToolDefinition? FindByName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        return All.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal));
    }

    // Always available (OutGame + InGame)
    public static readonly ToolDefinition WebSearch = new()
    {
        Name = "web_search",
        DisplayName = "Web Search",
        Description = "Search the web for game guides, wiki information, or strategy tips.",
        Icon = "tool_search.svg",
        ActionLabel = "Searched Internet",
        ParametersSchema = """{"type":"object","properties":{"query":{"type":"string","description":"Search query text"}},"required":["query"]}""",
        RequiresInGame = false
    };

    public static readonly ToolDefinition PlayerHistory = new()
    {
        Name = "player_history",
        DisplayName = "Player History",
        Description = "Retrieve past gaming sessions and match history.",
        Icon = "tool_history.png",
        ActionLabel = "Checked history",
        ParametersSchema = """{"type":"object","properties":{"username":{"type":"string","description":"Player username"},"game_type":{"type":"string","description":"Filter by game type"}},"required":["username"]}""",
        RequiresInGame = false
    };

    public static readonly ToolDefinition PlayerAnalytics = new()
    {
        Name = "player_analytics",
        DisplayName = "Player Analytics",
        Description = "Get win rates, performance trends, and statistical analysis.",
        Icon = "tool_analytics.png",
        ActionLabel = "Analyzed trends",
        ParametersSchema = """{"type":"object","properties":{"username":{"type":"string","description":"Player username"},"metric":{"type":"string","description":"Specific metric to analyze"}},"required":["username"]}""",
        RequiresInGame = false
    };

    // In-game only — Brain's live tools (parameterless cache reads)
    public static readonly ToolDefinition CaptureScreen = new()
    {
        Name = "capture_screen",
        DisplayName = "Capture Screen",
        Description = "Capture the current game screen for analysis.",
        Icon = "tool_capture.png",
        ActionLabel = "Captured screen",
        ParametersSchema = """{"type":"object","properties":{}}""",
        RequiresInGame = true
    };

    public static readonly ToolDefinition GetGameState = new()
    {
        Name = "get_game_state",
        DisplayName = "Game State",
        Description = "Get the current game state including position, score, and recent actions.",
        Icon = "tool_gamestate.png",
        ActionLabel = "Checked game state",
        ParametersSchema = """{"type":"object","properties":{}}""",
        RequiresInGame = true
    };

    // ── Chess-specific tools (Phase 09) ──────────────────────────────────

    public static readonly ToolDefinition AnalyzePositionEngine = new()
    {
        Name = "analyze_position_engine",
        DisplayName = "Engine Analysis",
        Description = "Run Stockfish chess engine analysis on a position. Returns best move, evaluation, and top candidate moves with continuations. Use for tactical accuracy and concrete move recommendations.",
        Icon = "tool_engine.png",
        ActionLabel = "Ran engine analysis",
        ParametersSchema = """{"type":"object","properties":{"fen":{"type":"string","description":"FEN string of current board position"},"depth":{"type":"integer","description":"Max search depth (default 25)"},"num_lines":{"type":"integer","description":"Number of candidate moves to return (1-5, default 3)"}},"required":["fen"]}""",
        RequiresInGame = true
    };

    public static readonly ToolDefinition AnalyzePositionStrategic = new()
    {
        Name = "analyze_position_strategic",
        DisplayName = "Strategic Analysis",
        Description = "Perform strategic chess analysis: pawn structure, piece activity, king safety, plans, and themes. Use when explaining ideas or long-term plans rather than calculating specific moves.",
        Icon = "tool_strategy.png",
        ActionLabel = "Reviewed strategy",
        ParametersSchema = """{"type":"object","properties":{"focus":{"type":"string","description":"Analysis focus: general, attack, defense, endgame, pawn_structure, piece_activity"},"player_color":{"type":"string","description":"Which side the player is playing: white or black"}},"required":[]}""",
        RequiresInGame = true
    };

    // ── Game Journal (Phase 14 / RASA generalized) ────────────────────────

    public static readonly ToolDefinition GameJournal = new()
    {
        Name = "game_journal",
        DisplayName = "Game Journal",
        Description = "Log or retrieve gameplay events, decisions, observations, and patterns. Use 'add' to log new entries, 'query' to search past entries, 'summary' to get session recap. For chess agents, returns move-by-move journal with FEN positions when no action is specified.",
        Icon = "history_clock.png",
        ActionLabel = "Updating journal",
        ParametersSchema = """{"type":"object","properties":{"action":{"type":"string","enum":["add","query","summary"],"description":"add: log entry. query: search. summary: session recap. Omit for chess read-only mode."},"entry_type":{"type":"string","enum":["event","decision","observation","pattern","correction"],"description":"Category (add action only)."},"content":{"type":"string","description":"Entry text or search query."},"tags":{"type":"array","items":{"type":"string"},"description":"Optional tags."}},"required":[]}""",
        RequiresInGame = true
    };

    // ── Replay Search (Phase 3) ───────────────────────────────────────────

    public static readonly ToolDefinition SearchReplay = new()
    {
        Name = "search_replay",
        DisplayName = "Replay Search",
        Description = "Search recent gameplay footage for specific events, moments, or patterns. Checks cached analysis first (instant), then analyzes raw footage on miss (30-120s). Use for questions about past gameplay like 'how did I die', 'show me that play', 'what happened at B site'.",
        Icon = "tool_history.png",
        ActionLabel = "Searching footage",
        ParametersSchema = """{"type":"object","properties":{"query":{"type":"string","description":"What to search for in gameplay footage"},"time_hint":{"type":"string","description":"Optional time context like 'last 2 minutes' or 'round 3'"}},"required":["query"]}""",
        RequiresInGame = false
    };

    // ── Show Replay (Media Cards) ─────────────────────────────────────────

    public static readonly ToolDefinition ShowReplay = new()
    {
        Name = "show_replay",
        DisplayName = "Show Replay",
        Description = "Show a short video replay clip. Use when the user asks to see something that just happened, or when pointing out a notable moment.",
        Icon = "tool_history.png",
        ActionLabel = "Showing replay",
        ParametersSchema = """{"type":"object","properties":{"timestamp":{"type":"string","description":"When to start the clip. Absolute session time ('2:15'), relative ('now-30s'), or anchor ('last_kill', 'last_death')."},"duration":{"type":"integer","description":"Clip length in seconds. Default 30, max 60.","default":30},"title":{"type":"string","description":"Optional title shown above the video (e.g. 'THAT FLANK', 'WATCH THIS')"}},"required":["timestamp"]}""",
        RequiresInGame = true
    };

    // ── Gaimer Team (Phase A) ─────────────────────────────────────────────

    public static readonly ToolDefinition DelegateToTeam = new()
    {
        Name = "delegate_to_team",
        DisplayName = "Ghost Team",
        Description = "Hand off a task to Ghost Team for background research, file operations, or anything beyond local game context. Fire-and-forget — result arrives later via voice narration.",
        Icon = "tool_generic.png",
        ActionLabel = "Handed to team",
        ParametersSchema = """{"type":"object","properties":{"task":{"type":"string","description":"What to hand off"},"response_format":{"type":"string","description":"voice (2-3 sentences) or detailed (full explanation)"}},"required":["task"]}""",
        RequiresInGame = false
    };
}
