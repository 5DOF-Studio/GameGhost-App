using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;

namespace WitnessDesktop.Models;

public enum MessageRole
{
    User,       // Player typed something
    Assistant,  // Brain responding to user
    System,     // Internal context (not displayed, but in LLM history)
    Proactive   // Brain-initiated (blunder alert, opportunity, etc.)
}

public enum MessageIntent
{
    GeneralChat,      // Casual talk
    GamingHistory,    // "How did I do last session?"
    Analytics,        // "What's my win rate?"
    LiveGameInfo,     // "What should I do?" (IN-GAME ONLY)
    ImageAnalysis,    // Brain analyzed a screen capture
    BrainAlert,       // Proactive: blunder, opportunity, danger
    ToolResult        // Internal: tool call result injected into context
}

[Obsolete("Use MessageRole instead")]
public enum ChatMessageType
{
    AiInsight,
    Warning,
    Lore,
    User,
    System
}

public enum DeliveryState
{
    None,     // Not applicable (e.g. AI messages)
    Pending,
    Sent,
    Failed
}

public partial class ChatMessage : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public MessageRole Role { get; set; }
    public MessageIntent Intent { get; set; }
    
    [Obsolete("Use Role instead")]
    public ChatMessageType Type { get; set; }
    
    public string Content { get; set; } = string.Empty;
    
    [Obsolete("Use Content instead")]
    public string? Text { get; set; }
    
    public byte[]? Image { get; set; }
    public ImageSource? ImageSource { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; }
    
    public BrainMetadata? Brain { get; set; }
    public ToolCallInfo? ToolCall { get; set; }

    [ObservableProperty]
    private DeliveryState _deliveryState = DeliveryState.None;
}
