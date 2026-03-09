namespace GaimerDesktop.Models;

public class ToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ParametersSchema { get; init; } = "{}";
    public bool RequiresInGame { get; init; }
}

public static class ToolDefinitions
{
    // Always available (OutGame + InGame)
    public static readonly ToolDefinition WebSearch = new()
    {
        Name = "web_search",
        Description = "Search the web for game guides, wiki information, or strategy tips.",
        ParametersSchema = """{"type":"object","properties":{"query":{"type":"string","description":"Search query text"}},"required":["query"]}""",
        RequiresInGame = false
    };

    public static readonly ToolDefinition PlayerHistory = new()
    {
        Name = "player_history",
        Description = "Retrieve past gaming sessions and match history.",
        ParametersSchema = """{"type":"object","properties":{"username":{"type":"string","description":"Player username"},"game_type":{"type":"string","description":"Filter by game type"}},"required":["username"]}""",
        RequiresInGame = false
    };

    public static readonly ToolDefinition PlayerAnalytics = new()
    {
        Name = "player_analytics",
        Description = "Get win rates, performance trends, and statistical analysis.",
        ParametersSchema = """{"type":"object","properties":{"username":{"type":"string","description":"Player username"},"metric":{"type":"string","description":"Specific metric to analyze"}},"required":["username"]}""",
        RequiresInGame = false
    };

    // In-game only — Brain's live tools (parameterless cache reads)
    public static readonly ToolDefinition CaptureScreen = new()
    {
        Name = "capture_screen",
        Description = "Capture the current game screen for analysis.",
        ParametersSchema = """{"type":"object","properties":{}}""",
        RequiresInGame = true
    };

    public static readonly ToolDefinition GetBestMove = new()
    {
        Name = "get_best_move",
        Description = "Get the Brain's recommended next action based on current position.",
        ParametersSchema = """{"type":"object","properties":{}}""",
        RequiresInGame = true
    };

    public static readonly ToolDefinition GetGameState = new()
    {
        Name = "get_game_state",
        Description = "Get the current game state including position, score, and recent actions.",
        ParametersSchema = """{"type":"object","properties":{}}""",
        RequiresInGame = true
    };

    // ── Chess-specific tools (Phase 09) ──────────────────────────────────

    public static readonly ToolDefinition AnalyzePositionEngine = new()
    {
        Name = "analyze_position_engine",
        Description = "Run Stockfish chess engine analysis on a position. Returns best move, evaluation, and top candidate moves with continuations. Use for tactical accuracy and concrete move recommendations.",
        ParametersSchema = """{"type":"object","properties":{"fen":{"type":"string","description":"FEN string of current board position"},"depth":{"type":"integer","description":"Max search depth (default 25)"},"num_lines":{"type":"integer","description":"Number of candidate moves to return (1-5, default 3)"}},"required":["fen"]}""",
        RequiresInGame = true
    };

    public static readonly ToolDefinition AnalyzePositionStrategic = new()
    {
        Name = "analyze_position_strategic",
        Description = "Perform strategic chess analysis: pawn structure, piece activity, king safety, plans, and themes. Use when explaining ideas or long-term plans rather than calculating specific moves.",
        ParametersSchema = """{"type":"object","properties":{"focus":{"type":"string","description":"Analysis focus: general, attack, defense, endgame, pawn_structure, piece_activity"},"player_color":{"type":"string","description":"Which side the player is playing: white or black"}},"required":[]}""",
        RequiresInGame = true
    };
}
