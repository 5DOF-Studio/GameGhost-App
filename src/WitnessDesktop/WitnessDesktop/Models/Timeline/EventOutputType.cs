namespace WitnessDesktop.Models.Timeline;

public enum EventOutputType
{
    // === GENERIC (All Agents) ===
    
    // In-game outputs
    Danger,           // High-risk alert (blunder, low HP, ambush)
    Opportunity,      // Advantageous action available
    SageAdvice,       // Tactical guidance, best moves, build advice
    Assessment,       // Generic state evaluation
    GameStateChange,  // Phase/state transition (new game, round end, etc.)
    Detection,        // User-primed vision detection (call out if seen)

    // Both contexts
    DirectMessage,    // User typed a message + Brain response
    ImageAnalysis,    // Screen analysis text output

    // Out-game outputs
    AnalyticsResult,  // Win rate, performance stats
    HistoryRecall,    // Past session reference
    GeneralChat,      // Casual conversation
    
    // === AGENT-SPECIFIC MARKER ===
    AgentSpecific = 100
}
