using Microsoft.Maui.Controls;

namespace WitnessDesktop.Models;

public class SlidingPanelContent
{
    public required string Title { get; init; }
    public required string Text { get; init; }
    public byte[]? Image { get; init; }
    public ImageSource? ImageSource { get; init; }
    public int AutoDismissMs { get; init; } = 5000;

    /// <summary>Tool call metadata for tool-specific ghost card layout.</summary>
    public ToolCallInfo? ToolCall { get; init; }

    /// <summary>True when this card should render with the centered-icon tool layout.</summary>
    public bool IsToolCall => ToolCall != null;

    /// <summary>Tool icon path for the centered icon visual. Falls back to generic.</summary>
    public string ToolIconPath => ToolCall?.Icon ?? "tool_generic.png";
}

