using System.Text;
using GaimerDesktop.Models;

namespace GaimerDesktop.Services;

/// <summary>
/// Brain context envelope builder with L1 event store, L2 rolling summary, and deterministic token budgeting.
/// Spec: BRAIN_CONTEXT_PIPELINE_SPEC -- L1 immediate (0-30s), L2 rolling (30s-5min), L3 session narrative (future).
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

    // L1 event store
    private readonly List<BrainEvent> _l1Events = new();
    private readonly object _l1Lock = new();
    private const int MaxL1Events = 200; // cap to prevent unbounded growth
    private static readonly TimeSpan L1Window = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan L1RetentionWindow = TimeSpan.FromMinutes(5); // keep for L2 generation

    // Priority categories for L1 budgeting (threat/objective ranked first)
    private static readonly HashSet<string> HighPriorityCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "threat", "objective", "critical", "danger", "warning"
    };

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
    public Task IngestEventAsync(BrainEvent evt, CancellationToken ct = default)
    {
        lock (_l1Lock)
        {
            _l1Events.Add(evt);

            // Prune events older than retention window (5 min)
            var cutoff = DateTime.UtcNow - L1RetentionWindow;
            _l1Events.RemoveAll(e => e.TimestampUtc < cutoff);

            // Cap total count
            while (_l1Events.Count > MaxL1Events)
                _l1Events.RemoveAt(0);
        }
        return Task.CompletedTask;
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

        if (envelope.ImmediateEvents.Count > 0)
        {
            sb.AppendLine("--- Immediate events ---");
            foreach (var evt in envelope.ImmediateEvents.Take(10))
                sb.AppendLine($"  [{evt.TimestampUtc:HH:mm:ss}] {evt.Category}: {evt.Text}");
        }

        if (!string.IsNullOrWhiteSpace(envelope.RecentChatSummary))
        {
            sb.AppendLine("--- Recent chat ---");
            sb.AppendLine(envelope.RecentChatSummary);
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

        // --- 1. Always include active target metadata (highest priority) ---
        var targetBlock = FormatActiveTarget(activeTarget);
        var targetTokens = EstimateTokens(targetBlock);
        var allocated = 0;

        if (!string.IsNullOrEmpty(targetBlock))
            allocated += targetTokens;

        // --- 2. L1 events (0-30s window, confidence >= 0.3, not stale) ---
        List<BrainEvent> l1Events;
        lock (_l1Lock)
        {
            var l1Cutoff = requestTsUtc - L1Window;
            l1Events = _l1Events
                .Where(e => e.TimestampUtc >= l1Cutoff)
                .Where(e => e.Confidence >= 0.3) // filter low-confidence
                .Where(e => e.ValidFor == null || e.TimestampUtc + e.ValidFor.Value >= requestTsUtc) // not stale
                .OrderByDescending(e => e.TimestampUtc)
                .ToList();
        }

        // Budget L1 events (newest first, prioritize threat/objective categories)
        var l1Prioritized = l1Events
            .OrderByDescending(e => HighPriorityCategories.Contains(e.Category) ? 1 : 0)
            .ThenByDescending(e => e.TimestampUtc)
            .ToList();

        var budgetRemaining = budgetTokens - allocated;
        var l1Included = new List<BrainEvent>();
        var l1Total = l1Prioritized.Count;
        foreach (var evt in l1Prioritized)
        {
            if (budgetRemaining <= 0) break;
            var tok = EstimateTokens(evt.Text) + 5; // overhead for timestamp+category prefix
            if (tok <= budgetRemaining)
            {
                l1Included.Add(evt);
                allocated += tok;
                budgetRemaining -= tok;
            }
        }
        if (l1Included.Count < l1Total && l1Total > 0)
            report.Add($"L1: {l1Included.Count}/{l1Total} events");

        // Re-sort included events chronologically for display
        l1Included = l1Included.OrderByDescending(e => e.TimestampUtc).ToList();

        // --- 3. Recent chat (newest first), truncate oldest when over budget ---
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

        budgetRemaining = budgetTokens - allocated;
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

        // --- 4. L2 rolling summary (30s-5min window, deterministic category-grouped text) ---
        List<BrainEvent> l2Events;
        lock (_l1Lock)
        {
            var l2Start = requestTsUtc - TimeSpan.FromMinutes(5);
            var l2End = requestTsUtc - L1Window; // events older than 30s
            l2Events = _l1Events
                .Where(e => e.TimestampUtc >= l2Start && e.TimestampUtc < l2End)
                .OrderByDescending(e => e.TimestampUtc)
                .ToList();
        }

        var rollingSummary = BuildL2Summary(l2Events);
        var l2Tokens = EstimateTokens(rollingSummary);
        budgetRemaining = budgetTokens - allocated;
        if (l2Tokens > budgetRemaining)
        {
            // Truncate L2 to fit budget
            if (budgetRemaining > 0)
            {
                var maxChars = budgetRemaining * CharsPerToken;
                rollingSummary = rollingSummary.Length > maxChars
                    ? rollingSummary[..maxChars] + "..."
                    : rollingSummary;
                allocated += EstimateTokens(rollingSummary);
                report.Add($"L2: truncated ({l2Tokens} -> {EstimateTokens(rollingSummary)} tokens)");
            }
            else
            {
                rollingSummary = string.Empty;
                report.Add($"L2: dropped (no budget remaining)");
            }
        }
        else
        {
            allocated += l2Tokens;
        }

        // --- 5. Reel refs (high confidence first) ---
        var orderedReels = reelMoments
            .OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => m.TimestampUtc)
            .ToList();

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

        // --- 6. L3 session narrative (future -- Phase B per spec) ---
        var sessionNarrative = (string?)null;

        return new SharedContextEnvelope
        {
            RequestTsUtc = requestTsUtc,
            Intent = intent,
            BudgetTokens = budgetTokens,
            ImmediateEvents = l1Included,
            RollingSummary = rollingSummary,
            SessionNarrative = sessionNarrative,
            ReelRefs = orderedReels.Take(reelIncluded).ToList(),
            TruncationReport = report.Count > 0 ? string.Join("; ", report) : "none",
            EnvelopeConfidence = ComputeEnvelopeConfidence(l1Included, l2Events),
            ActiveTargetMetadata = targetBlock,
            RecentChatSummary = recentChatSummary
        };
    }

    /// <summary>
    /// Build a deterministic L2 rolling summary from events in the 30s-5min window.
    /// Groups by category, counts occurrences, shows latest text per category.
    /// </summary>
    private static string BuildL2Summary(List<BrainEvent> l2Events)
    {
        if (l2Events.Count == 0)
            return string.Empty;

        var grouped = l2Events
            .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "general" : e.Category)
            .OrderByDescending(g => HighPriorityCategories.Contains(g.Key) ? 1 : 0)
            .ThenByDescending(g => g.Count());

        var sb = new StringBuilder();
        foreach (var group in grouped)
        {
            var latest = group.OrderByDescending(e => e.TimestampUtc).First();
            sb.AppendLine($"{group.Key}: {group.Count()} events. Latest: {latest.Text}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Compute envelope confidence based on L1/L2 event coverage.
    /// Higher when more high-confidence recent events are available.
    /// </summary>
    private static double ComputeEnvelopeConfidence(List<BrainEvent> l1Included, List<BrainEvent> l2Events)
    {
        if (l1Included.Count == 0 && l2Events.Count == 0)
            return 0.5; // no events, moderate confidence (reel refs and chat only)

        var totalEvents = l1Included.Count + l2Events.Count;
        var avgConfidence = l1Included.Concat(l2Events)
            .Average(e => e.Confidence);

        // Scale: 0.5 base + up to 0.4 from event confidence + 0.1 from having L1 events
        var hasL1Bonus = l1Included.Count > 0 ? 0.1 : 0.0;
        return Math.Min(1.0, 0.5 + (avgConfidence * 0.4) + hasL1Bonus);
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
