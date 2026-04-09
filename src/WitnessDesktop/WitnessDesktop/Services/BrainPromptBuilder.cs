using System.Text;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Services;

/// <summary>
/// Assembles structured system and user prompts for the brain image analysis pipeline.
/// System prompt: identity + game-knowledge (from active pack or generic fallback) + game context + previous game.
/// User prompt: minimal text alongside the image, or pack-driven template.
/// </summary>
public sealed class BrainPromptBuilder : IBrainPromptBuilder
{
    private const int MaxL1Events = 5;
    private readonly IGameSkillPackService? _packService;

    public BrainPromptBuilder(IGameSkillPackService? packService = null)
    {
        _packService = packService;
    }

    /// <inheritdoc />
    public string BuildSystemPrompt(Agent agent, IReadOnlyList<BrainEvent> l1Events,
        string? journalSummary, string? rollingSummary, string? previousGameSummary,
        bool isConnectedToGame)
    {
        var sb = new StringBuilder();

        // 1. [IDENTITY] — agent personality prefix (includes vision + language rules from Plan 04)
        if (!string.IsNullOrWhiteSpace(agent.BrainPersonalityPrefix))
        {
            sb.AppendLine(agent.BrainPersonalityPrefix.Trim());
            sb.AppendLine();
        }

        // 2. [GAME KNOWLEDGE] — pack brain instructions or generic fallback
        var pack = _packService?.ActivePack;
        if (pack != null && !string.IsNullOrWhiteSpace(pack.BrainInstructionsContent))
        {
            sb.AppendLine(pack.BrainInstructionsContent.Trim());
            sb.AppendLine();
        }
        else
        {
            // Generic fallback — no pack loaded
            sb.AppendLine("[INSTRUCTIONS]");
            sb.AppendLine("Describe what you see in the image. Provide an assessment of the current state.");
            sb.AppendLine("Tag your confidence: CERTAIN, LIKELY, UNCERTAIN, or GUESSING.");
            sb.AppendLine();
        }

        // Connection state awareness — only when NOT connected
        if (!isConnectedToGame)
        {
            var gameName = pack?.Name ?? "a game";
            sb.AppendLine("[CONNECTION STATUS]");
            sb.AppendLine($"You are not currently connected to {gameName}. You cannot see the screen right now.");
            sb.AppendLine("When the conversation naturally calls for gameplay analysis or advice,");
            sb.AppendLine("gently encourage the player to connect so you can watch and help.");
            sb.AppendLine("You can discuss game concepts and strategy freely without a connection.");
            sb.AppendLine("Do not force the suggestion — let it arise naturally from context.");
            sb.AppendLine();
        }

        // 4. [GAME CONTEXT] — dynamic, assembled from inputs
        sb.AppendLine("--- Game Journal ---");
        sb.AppendLine(journalSummary ?? "No positions recorded yet.");
        sb.AppendLine();

        sb.AppendLine("--- Recent Observations (last 30s) ---");
        sb.AppendLine(BrainContextFormatter.FormatL1Events(l1Events, MaxL1Events));
        sb.AppendLine();

        sb.AppendLine("--- Rolling Summary (30s-5min) ---");
        sb.AppendLine(rollingSummary ?? "No rolling summary available.");
        sb.AppendLine();

        // 5. [PREVIOUS GAME] — only if provided
        if (!string.IsNullOrEmpty(previousGameSummary))
        {
            sb.AppendLine("--- Previous Game ---");
            sb.AppendLine(previousGameSummary);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <inheritdoc />
    public string BuildUserPrompt(string gameType, int moveNumber)
    {
        var pack = _packService?.ActivePack;
        if (pack?.UserPromptTemplate != null)
        {
            return pack.UserPromptTemplate
                .Replace("{moveNumber}", moveNumber.ToString())
                .Replace("{gameType}", gameType)
                .Replace("{genre}", pack.Genre);
        }
        return "Analyze this frame.";
    }

    /// <inheritdoc />
    public string AssembleReplayContext(ReplayContext replayContext, int maxTokenBudget = 600)
    {
        var sb = new StringBuilder();

        // Header with time window
        sb.AppendLine($"[REPLAY CONTEXT: {replayContext.WindowStartUtc:HH:mm:ss} to {replayContext.WindowEndUtc:HH:mm:ss}]");

        if (replayContext.Items.Count == 0)
            return sb.ToString().TrimEnd();

        // Format each item chronologically
        var itemLines = new List<string>();
        foreach (var item in replayContext.Items)
        {
            var timestamp = item.TimestampUtc.ToString("HH:mm:ss");
            switch (item.Kind)
            {
                case ReplayItemKind.ChatMessage:
                    itemLines.Add($"[{timestamp}] {item.MessageRole}: {item.MessageContent}");
                    break;

                case ReplayItemKind.TimelineEvent:
                    var signal = !string.IsNullOrEmpty(item.BrainSignal)
                        ? $" (signal={item.BrainSignal})"
                        : "";
                    itemLines.Add($"[{timestamp}] [{item.EventType}] {item.EventSummary}{signal}");
                    break;

                case ReplayItemKind.CaptureArtifact:
                    var availability = item.ArtifactExists ? "image available" : "image expired";
                    itemLines.Add($"[{timestamp}] [CAPTURE] {availability}");
                    break;
            }
        }

        // Build full text and check budget (1 token ~ 4 chars)
        var maxChars = maxTokenBudget * 4;
        var headerLength = sb.Length;
        var truncationMarker = "... (truncated, showing most recent)";

        // Add lines, checking if we exceed budget
        var allLines = string.Join(Environment.NewLine, itemLines);
        if (headerLength + allLines.Length <= maxChars)
        {
            sb.Append(allLines);
        }
        else
        {
            // Truncate: keep most recent items (from the end)
            var remaining = maxChars - headerLength - truncationMarker.Length - Environment.NewLine.Length;
            var included = new List<string>();

            for (int i = itemLines.Count - 1; i >= 0; i--)
            {
                var lineLen = itemLines[i].Length + Environment.NewLine.Length;
                if (remaining - lineLen < 0)
                    break;
                remaining -= lineLen;
                included.Insert(0, itemLines[i]);
            }

            sb.AppendLine(truncationMarker);
            sb.Append(string.Join(Environment.NewLine, included));
        }

        return sb.ToString().TrimEnd();
    }
}
