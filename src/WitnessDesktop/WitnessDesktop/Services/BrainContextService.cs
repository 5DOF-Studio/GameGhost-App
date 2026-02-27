using System.Text;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// MVP implementation of brain context envelope builder with deterministic token budgeting.
/// GMR-009, GMR-010: recent chat + reel moments + active target; truncation with priority order.
/// </summary>
public sealed class BrainContextService : IBrainContextService
{
    private readonly IVisualReelService _visualReelService;

    /// <summary>Default voice budget tokens (spec).</summary>
    public const int DefaultVoiceBudget = 900;

    /// <summary>Default chat budget tokens (spec).</summary>
    public const int DefaultChatBudget = 1200;

    /// <summary>Hard max tokens (spec).</summary>
    public const int HardMaxTokens = 1600;

    /// <summary>Approximate chars per token for budgeting.</summary>
    private const int CharsPerToken = 4;

    public BrainContextService(IVisualReelService visualReelService)
    {
        _visualReelService = visualReelService;
    }

    /// <inheritdoc />
    public Task<SharedContextEnvelope> GetContextForVoiceAsync(
        DateTime requestTsUtc,
        string intent = "general",
        int budgetTokens = DefaultVoiceBudget,
        ContextAssemblyInputs? inputs = null,
        CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(requestTsUtc, intent, Math.Min(budgetTokens, HardMaxTokens), inputs);
        return Task.FromResult(envelope);
    }

    /// <inheritdoc />
    public Task<SharedContextEnvelope> GetContextForChatAsync(
        DateTime requestTsUtc,
        string intent = "general",
        int budgetTokens = DefaultChatBudget,
        ContextAssemblyInputs? inputs = null,
        CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(requestTsUtc, intent, Math.Min(budgetTokens, HardMaxTokens), inputs);
        return Task.FromResult(envelope);
    }

    /// <inheritdoc />
    public string FormatAsPrefixedContextBlock(SharedContextEnvelope envelope)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Context]");
        sb.AppendLine($"Request: {envelope.RequestTsUtc:O} | Intent: {envelope.Intent} | Budget: {envelope.BudgetTokens} tokens");
        if (!string.IsNullOrEmpty(envelope.TruncationReport))
            sb.AppendLine($"Truncation: {envelope.TruncationReport}");

        if (!string.IsNullOrWhiteSpace(envelope.ActiveTargetMetadata))
        {
            sb.AppendLine("--- Target ---");
            sb.AppendLine(envelope.ActiveTargetMetadata);
        }

        if (!string.IsNullOrWhiteSpace(envelope.RecentChatSummary))
        {
            sb.AppendLine("--- Recent chat ---");
            sb.AppendLine(envelope.RecentChatSummary);
        }

        if (envelope.ImmediateEvents.Count > 0)
        {
            sb.AppendLine("--- Immediate events ---");
            foreach (var evt in envelope.ImmediateEvents.Take(10))
                sb.AppendLine($"  [{evt.TimestampUtc:HH:mm:ss}] {evt.Category}: {evt.Text}");
        }

        if (!string.IsNullOrWhiteSpace(envelope.RollingSummary))
        {
            sb.AppendLine("--- Rolling summary ---");
            sb.AppendLine(envelope.RollingSummary);
        }

        if (envelope.ReelRefs.Count > 0)
        {
            sb.AppendLine("--- Reel refs ---");
            foreach (var m in envelope.ReelRefs.Take(5))
                sb.AppendLine($"  {m.TimestampUtc:HH:mm:ss} | {m.SourceTarget} | conf={m.Confidence:F2}");
        }

        if (!string.IsNullOrWhiteSpace(envelope.SessionNarrative))
        {
            sb.AppendLine("--- Session narrative ---");
            sb.AppendLine(envelope.SessionNarrative);
        }

        sb.AppendLine("[End Context]");
        return sb.ToString();
    }

    private SharedContextEnvelope BuildEnvelope(
        DateTime requestTsUtc,
        string intent,
        int budgetTokens,
        ContextAssemblyInputs? inputs)
    {
        var report = new List<string>();
        var reelMoments = _visualReelService.GetRecent(20);
        var recentChat = inputs?.RecentChat ?? [];
        var activeTarget = inputs?.ActiveTarget;

        // Priority order (spec): active target, last user intent, L1, L2, reel refs, L3
        var targetBlock = FormatActiveTarget(activeTarget);
        var targetTokens = EstimateTokens(targetBlock);

        var chatLines = new List<string>();
        foreach (var msg in recentChat.Reverse().Take(20))
        {
            var role = msg.Role switch
            {
                MessageRole.User => "User",
                MessageRole.Assistant => "AI",
                MessageRole.System => "System",
                MessageRole.Proactive => "AI",
                _ => "Other"
            };
            if (!string.IsNullOrWhiteSpace(msg.Content))
                chatLines.Add($"{role}: {msg.Content}");
        }

        var orderedReels = reelMoments
            .OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => m.TimestampUtc)
            .ToList();

        var allocated = 0;

        // 1. Always include active target metadata
        if (!string.IsNullOrEmpty(targetBlock))
            allocated += targetTokens;

        // 2. Recent chat (newest first), truncate oldest when over budget
        var budgetRemaining = budgetTokens - allocated;
        var chatIncluded = 0;
        var chatSummaryLines = new List<string>();
        for (var i = chatLines.Count - 1; i >= 0 && budgetRemaining > 0; i--)
        {
            var line = chatLines[i];
            var tok = EstimateTokens(line);
            if (tok <= budgetRemaining)
            {
                chatSummaryLines.Add(line);
                allocated += tok;
                budgetRemaining -= tok;
                chatIncluded++;
            }
        }
        if (chatIncluded < chatLines.Count)
            report.Add($"chat: {chatIncluded}/{chatLines.Count} turns");

        var recentChatSummary = string.Join(Environment.NewLine, chatSummaryLines.AsEnumerable().Reverse());

        // 3. Reel refs (high confidence first)
        budgetRemaining = budgetTokens - allocated;
        var reelIncluded = 0;
        foreach (var m in orderedReels)
        {
            if (budgetRemaining <= 0) break;
            var desc = $"Reel[{m.TimestampUtc:HH:mm:ss}] {m.SourceTarget}";
            var tok = EstimateTokens(desc);
            if (tok <= budgetRemaining)
            {
                allocated += tok;
                budgetRemaining -= tok;
                reelIncluded++;
            }
        }
        if (reelIncluded < orderedReels.Count && orderedReels.Count > 0)
            report.Add($"reel: {reelIncluded}/{orderedReels.Count} moments");

        // L2/L3 empty for MVP
        var rollingSummary = string.Empty;
        var sessionNarrative = (string?)null;

        return new SharedContextEnvelope
        {
            RequestTsUtc = requestTsUtc,
            Intent = intent,
            BudgetTokens = budgetTokens,
            ImmediateEvents = [], // No L1 event store in MVP
            RollingSummary = rollingSummary,
            SessionNarrative = sessionNarrative,
            ReelRefs = orderedReels.Take(reelIncluded).ToList(),
            TruncationReport = report.Count > 0 ? string.Join("; ", report) : "none",
            EnvelopeConfidence = 0.9,
            ActiveTargetMetadata = targetBlock,
            RecentChatSummary = recentChatSummary
        };
    }

    private static string FormatActiveTarget(CaptureTarget? target)
    {
        if (target == null) return string.Empty;
        return $"[Target] {target.ProcessName} | {target.WindowTitle}";
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return Math.Max(1, text.Length / CharsPerToken);
    }
}
