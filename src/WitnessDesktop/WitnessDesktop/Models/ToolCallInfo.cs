namespace WitnessDesktop.Models;

public class ToolCallInfo
{
    public string ToolName { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }

    public string DisplayName => ToolDefinitions.FindByName(ToolName)?.DisplayName ?? ToolName;
    public string Icon => ToolDefinitions.FindByName(ToolName)?.Icon ?? "tool_generic.png";
    public string ActionLabel => ToolDefinitions.FindByName(ToolName)?.ActionLabel ?? DisplayName;
    public string SummaryText => Success ? ActionLabel : $"{DisplayName} failed";
    public bool HasDuration => DurationMs > 0;
    public string DurationLabel => DurationMs > 0 ? $"{DurationMs}ms" : string.Empty;
}
