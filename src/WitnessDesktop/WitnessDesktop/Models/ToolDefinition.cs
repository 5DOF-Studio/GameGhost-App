namespace WitnessDesktop.Models;

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
    
    // In-game only — Brain's live tools
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
