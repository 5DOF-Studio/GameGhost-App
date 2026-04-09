using System.Text;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Shared formatting helpers for brain context serialization.
/// Used by BrainPromptBuilder (system prompt) and ToolExecutor (team delegation).
/// </summary>
public static class BrainContextFormatter
{
    /// <summary>
    /// Formats L1 immediate events as "[HH:mm:ss] Category: Text" lines.
    /// </summary>
    public static string FormatL1Events(IReadOnlyList<BrainEvent> events, int maxEvents = 5)
    {
        if (events.Count == 0)
            return "No recent observations.";

        var sb = new StringBuilder();
        var taken = 0;
        foreach (var evt in events)
        {
            if (taken >= maxEvents) break;
            if (taken > 0) sb.AppendLine();
            sb.Append($"[{evt.TimestampUtc:HH:mm:ss}] {evt.Category}: {evt.Text}");
            taken++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Assembles recent activity from chat + voice transcript.
    /// Returns null if both are empty.
    /// </summary>
    public static string? FormatRecentActivity(string recentChat, string recentVoice)
    {
        var hasChat = !string.IsNullOrWhiteSpace(recentChat);
        var hasVoice = !string.IsNullOrWhiteSpace(recentVoice);

        if (!hasChat && !hasVoice)
            return null;

        var sb = new StringBuilder();
        if (hasChat)
        {
            sb.AppendLine("--- Recent Chat ---");
            sb.AppendLine(recentChat);
        }
        if (hasVoice)
        {
            if (hasChat) sb.AppendLine();
            sb.AppendLine("--- Recent Voice ---");
            sb.AppendLine(recentVoice);
        }
        return sb.ToString().TrimEnd();
    }
}
