namespace WitnessDesktop.Models;

public class ToolCallInfo
{
    public string ToolName { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }
}
