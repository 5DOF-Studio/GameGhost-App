namespace WitnessDesktop.Models.Timeline;

public static class EventIconMap
{
    private static readonly Dictionary<EventOutputType, string> GenericIcons = new()
    {
        [EventOutputType.Danger] = "alert.png",
        [EventOutputType.Opportunity] = "opportunity.png",
        [EventOutputType.SageAdvice] = "sage.png",
        [EventOutputType.Assessment] = "assessment.png",
        [EventOutputType.GameStateChange] = "new_game.png",
        [EventOutputType.Detection] = "detection.png",
        [EventOutputType.DirectMessage] = "chat.png",
        [EventOutputType.ImageAnalysis] = "video_reel.png",
        [EventOutputType.AnalyticsResult] = "performance.png",
        [EventOutputType.HistoryRecall] = "history_clock.png",
        [EventOutputType.GeneralChat] = "commentary_icon.png",
    };

    private static readonly Dictionary<EventOutputType, string> CapsuleColors = new()
    {
        [EventOutputType.Danger] = "#30ef4444",
        [EventOutputType.Opportunity] = "#30F2A900",
        [EventOutputType.SageAdvice] = "#3022c55e",
        [EventOutputType.Assessment] = "#304FC3F7",
        [EventOutputType.GameStateChange] = "#307F13EC",
        [EventOutputType.Detection] = "#30FF9800",
        [EventOutputType.DirectMessage] = "#30808080",
        [EventOutputType.ImageAnalysis] = "#3000BCD4",
        [EventOutputType.AnalyticsResult] = "#30B388FF",
        [EventOutputType.HistoryRecall] = "#309E9E9E",
        [EventOutputType.GeneralChat] = "#3026A69A",
    };

    private static readonly Dictionary<EventOutputType, string> CapsuleStrokes = new()
    {
        [EventOutputType.Danger] = "#50ef4444",
        [EventOutputType.Opportunity] = "#50F2A900",
        [EventOutputType.SageAdvice] = "#5022c55e",
        [EventOutputType.Assessment] = "#504FC3F7",
        [EventOutputType.GameStateChange] = "#507F13EC",
        [EventOutputType.Detection] = "#50FF9800",
        [EventOutputType.DirectMessage] = "#50808080",
        [EventOutputType.ImageAnalysis] = "#5000BCD4",
        [EventOutputType.AnalyticsResult] = "#50B388FF",
        [EventOutputType.HistoryRecall] = "#509E9E9E",
        [EventOutputType.GeneralChat] = "#5026A69A",
    };
    
    private static readonly Dictionary<ChessEventType, string> ChessIcons = new()
    {
        [ChessEventType.PositionEval] = "chessboard_eval.png",
        [ChessEventType.Tactic] = "chess_move.png",
    };
    
    private static readonly Dictionary<RpgEventType, string> RpgIcons = new()
    {
        [RpgEventType.QuestHint] = "quest.png",
        [RpgEventType.Lore] = "lore.png",
        [RpgEventType.ItemAlert] = "item_alert.png",
    };
    
    public static string GetIcon(EventOutputType type) =>
        GenericIcons.TryGetValue(type, out var icon) ? icon : "info.png";
    
    public static string GetIcon(ChessEventType type) =>
        ChessIcons.TryGetValue(type, out var icon) ? icon : "info.png";
    
    public static string GetIcon(RpgEventType type) =>
        RpgIcons.TryGetValue(type, out var icon) ? icon : "info.png";
    
    public static string GetIcon(int eventCode, AgentType agent)
    {
        if (eventCode < (int)EventOutputType.AgentSpecific)
            return GetIcon((EventOutputType)eventCode);

        return agent switch
        {
            AgentType.Chess => GetIcon((ChessEventType)eventCode),
            AgentType.General => GetIcon((RpgEventType)eventCode),
            _ => "info.png"
        };
    }

    public static string GetCapsuleColorHex(EventOutputType type) =>
        CapsuleColors.TryGetValue(type, out var hex) ? hex : "#30808080";

    public static string GetCapsuleStrokeHex(EventOutputType type) =>
        CapsuleStrokes.TryGetValue(type, out var hex) ? hex : "#50808080";
}
