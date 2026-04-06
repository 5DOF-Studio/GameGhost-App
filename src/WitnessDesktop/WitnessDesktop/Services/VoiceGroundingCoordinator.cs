using System.Text.RegularExpressions;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Deterministic voice grounding coordinator. Classifies user turns by factual requirement
/// and gates board-sensitive responses on fresh grounded brain context.
/// </summary>
public sealed class VoiceGroundingCoordinator : IVoiceGroundingCoordinator
{
    /// <summary>Maximum age before grounded context is considered stale.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(45);

    private readonly TimeSpan _maxAge;
    private readonly IGameSkillPackService? _packService;
    private volatile GroundedVoiceContext? _latestContext;

    // Cached compiled regexes from the active pack (rebuilt when pack changes)
    private string? _cachedPackId;
    private Regex? _packGameStateRegex;
    private Regex? _packGeneralKnowledgeRegex;

    public VoiceGroundingCoordinator(TimeSpan? maxAge = null, IGameSkillPackService? packService = null)
    {
        _maxAge = maxAge ?? DefaultMaxAge;
        _packService = packService;
    }

    public GroundedVoiceContext? LatestContext => _latestContext;

    public void UpdateGroundedContext(GroundedVoiceContext context)
    {
        _latestContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    public VoiceGroundingDecision Evaluate(string? userText, bool isInGame)
    {
        var turnClass = _packService != null ? ClassifyTurnWithPack(userText) : ClassifyTurn(userText);

        // Non-board-sensitive turns: answer directly without brain
        if (turnClass is VoiceTurnClass.Social or VoiceTurnClass.Control)
        {
            return new VoiceGroundingDecision(
                turnClass,
                VoiceResponseMode.AnswerDirectly,
                HasFreshGroundedContext: false,
                GroundedSummary: null,
                Reason: "Non-board-sensitive turn");
        }

        // History-sensitive and tool-dependent turns: defer to brain
        if (turnClass is VoiceTurnClass.HistorySensitive or VoiceTurnClass.ToolDependent)
        {
            return new VoiceGroundingDecision(
                turnClass,
                VoiceResponseMode.DeferToBrain,
                HasFreshGroundedContext: false,
                GroundedSummary: null,
                Reason: $"Turn requires brain capability ({turnClass})");
        }

        // General game questions: answer directly (knowledge-based, not board-state)
        if (turnClass == VoiceTurnClass.GeneralGameQuestion)
        {
            return new VoiceGroundingDecision(
                turnClass,
                VoiceResponseMode.AnswerDirectly,
                HasFreshGroundedContext: false,
                GroundedSummary: null,
                Reason: "General game knowledge, no board state needed");
        }

        // Board-sensitive turns: require grounded context
        var ctx = _latestContext;
        var nowUtc = DateTime.UtcNow;

        // No context ever received
        if (ctx == null)
        {
            return new VoiceGroundingDecision(
                turnClass,
                isInGame ? VoiceResponseMode.AcknowledgeAndRefresh : VoiceResponseMode.DeclineBoardCertainty,
                HasFreshGroundedContext: false,
                GroundedSummary: null,
                Reason: "No grounded context available");
        }

        // Context is stale
        if (ctx.IsStale(nowUtc, _maxAge))
        {
            return new VoiceGroundingDecision(
                turnClass,
                VoiceResponseMode.AcknowledgeUncertainty,
                HasFreshGroundedContext: false,
                GroundedSummary: null,
                Reason: $"Grounded context is stale ({(int)(nowUtc - ctx.CapturedAtUtc).TotalSeconds}s old)");
        }

        // Fresh grounded context available
        var summary = ctx.ToGroundingSummary();
        return new VoiceGroundingDecision(
            turnClass,
            VoiceResponseMode.RespondFromGroundedContext,
            HasFreshGroundedContext: true,
            GroundedSummary: string.IsNullOrEmpty(summary) ? null : summary,
            Reason: "Fresh grounded context available");
    }

    public string? GetGroundingPrefix()
    {
        var ctx = _latestContext;
        if (ctx == null) return null;

        var staleLabel = _packService?.ActivePack?.GroundingLanguage.StaleDisplay ?? "information may be outdated";
        if (ctx.IsStale(DateTime.UtcNow, _maxAge))
            return $"[GAME STATE: {staleLabel} — express uncertainty about specifics]";

        var summary = ctx.ToGroundingSummary();
        if (string.IsNullOrEmpty(summary)) return null;

        return $"[GROUNDED GAME STATE — use this as factual basis: {summary}]";
    }

    /// <summary>
    /// Formats a freshness signal for voice injection (spec Section 7.1).
    /// </summary>
    public static string FormatFreshnessPrefix(TimeSpan age)
    {
        if (age.TotalSeconds < 5) return "just now";
        if (age.TotalSeconds < 15) return $"from {(int)age.TotalSeconds} seconds ago";
        if (age.TotalSeconds < 30) return $"from about {(int)age.TotalSeconds} seconds ago — checking again now";
        return "that was a while back — let me get a fresh look";
    }

    // ── Turn Classification ──────────────────────────────────────────────

    // Board-sensitive patterns: questions about current board state, position, pieces
    private static readonly Regex BoardSensitiveRegex = new(
        @"\b(?:what(?:'s| is) (?:on |happening|the position|the board|my position|going on)|am i winning|am i losing|who(?:'s| is) (?:winning|ahead|better)|what (?:piece|pawn|knight|bishop|rook|queen|king)|where(?:'s| is) (?:my|the|his|her)|what (?:changed|moved|happened)|what(?:'s| is) the (?:eval|score|advantage)|should i (?:take|capture|move|play|castle|push)|best move|what do you (?:see|think about (?:this|the) position)|how(?:'s| is) (?:my|the) position|can (?:i|they) (?:fork|pin|checkmate|castle)|is (?:there|that) a (?:threat|fork|pin|check))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Control patterns: instructions to the AI itself
    private static readonly Regex ControlRegex = new(
        @"\b(?:be quiet|shut up|stop talking|speak (?:less|more|louder|softer)|be (?:quieter|louder|more (?:brief|verbose))|mute|unmute|volume|pause|resume)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Social patterns: conversational/emotional responses
    private static readonly Regex SocialRegex = new(
        @"\b(?:thanks|thank you|cool|nice|wow|haha|lol|ok|okay|sure|right|yeah|yep|got it|that(?:'s| is) (?:crazy|wild|insane|great|awesome|funny|interesting)|good (?:game|one)|gg|hello|hi|hey|bye|goodbye)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // History-sensitive patterns: questions about past gameplay events
    private static readonly Regex HistorySensitiveRegex = new(
        @"\b(?:what happened|how did (?:i|they|we) (?:die|lose|win)|earlier|last (?:round|game|match)|show me that|that play|replay|rewind|before that)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Tool-dependent patterns: questions requiring brain tools voice doesn't have
    private static readonly Regex ToolDependentRegex = new(
        @"\b(?:run the engine|(?:stockfish|engine) (?:says?|analysis|think)|analyze (?:this|the position)|check (?:my )?journal|search (?:for|the|replay))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // General game knowledge patterns: strategy/theory not requiring live board
    private static readonly Regex GeneralGameRegex = new(
        @"\b(?:what(?:'s| is) (?:a|an|the) (?:sicilian|french|caro.kann|king(?:'s)? indian|queen(?:'s)? gambit|london|italian|ruy lopez|english|dutch|pirc|scandinavian|alekhine)|how (?:does|do) (?:you|i) (?:play|counter|defend)|what(?:'s| is) the (?:idea|theory|plan) (?:behind|of|in)|explain|teach me|what are (?:the )?(?:principles|rules|basics)|tell me about|history of|who (?:invented|created|played))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Classify using pack-driven patterns when available.
    /// Instance method that checks the active pack's voice classification patterns.
    /// </summary>
    internal VoiceTurnClass ClassifyTurnWithPack(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return VoiceTurnClass.Unclear;

        // Universal patterns first (game-agnostic)
        if (ControlRegex.IsMatch(text))
            return VoiceTurnClass.Control;

        if (HistorySensitiveRegex.IsMatch(text))
            return VoiceTurnClass.HistorySensitive;

        if (ToolDependentRegex.IsMatch(text))
            return VoiceTurnClass.ToolDependent;

        // Pack-driven game-state patterns (replace hardcoded chess BoardSensitive)
        EnsurePackRegexCurrent();
        if (_packGameStateRegex?.IsMatch(text) == true)
            return VoiceTurnClass.BoardSensitive;

        if (_packGeneralKnowledgeRegex?.IsMatch(text) == true)
            return VoiceTurnClass.GeneralGameQuestion;

        // Fallback: generic patterns that work for any game
        if (GenericGameStateRegex.IsMatch(text))
            return VoiceTurnClass.BoardSensitive;

        if (SocialRegex.IsMatch(text))
            return VoiceTurnClass.Social;

        // Default: if in doubt and it looks like a question, treat as game-state-sensitive
        if (text.TrimEnd().EndsWith('?'))
            return VoiceTurnClass.BoardSensitive;

        return VoiceTurnClass.Unclear;
    }

    // Generic game-state patterns that work across ALL games (no chess jargon)
    private static readonly Regex GenericGameStateRegex = new(
        @"\b(?:what(?:'s| is) (?:happening|going on)|am i (?:winning|losing)|who(?:'s| is) (?:winning|ahead|better)|what (?:changed|happened)|what do you (?:see|think)|how(?:'s| is) (?:it going|the situation)|should i|what(?:'s| is) the (?:score|situation))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private void EnsurePackRegexCurrent()
    {
        var pack = _packService?.ActivePack;
        var packId = pack?.Id;

        if (packId == _cachedPackId) return; // Already current
        _cachedPackId = packId;

        if (pack?.VoiceClassification.GameStateSensitive is { Count: > 0 } gsPatterns)
        {
            var combined = string.Join("|", gsPatterns);
            _packGameStateRegex = new Regex(combined, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        else
        {
            _packGameStateRegex = null;
        }

        if (pack?.VoiceClassification.GeneralKnowledge is { Count: > 0 } gkPatterns)
        {
            var combined = string.Join("|", gkPatterns);
            _packGeneralKnowledgeRegex = new Regex(combined, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        else
        {
            _packGeneralKnowledgeRegex = null;
        }
    }

    /// <summary>
    /// Static classifier using hardcoded chess patterns. Kept for backward compat with tests.
    /// Production code should use ClassifyTurnWithPack via the instance Evaluate method.
    /// </summary>
    internal static VoiceTurnClass ClassifyTurn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return VoiceTurnClass.Unclear;

        if (ControlRegex.IsMatch(text))
            return VoiceTurnClass.Control;

        if (HistorySensitiveRegex.IsMatch(text))
            return VoiceTurnClass.HistorySensitive;

        if (ToolDependentRegex.IsMatch(text))
            return VoiceTurnClass.ToolDependent;

        if (BoardSensitiveRegex.IsMatch(text))
            return VoiceTurnClass.BoardSensitive;

        if (GeneralGameRegex.IsMatch(text))
            return VoiceTurnClass.GeneralGameQuestion;

        if (SocialRegex.IsMatch(text))
            return VoiceTurnClass.Social;

        // Default: if in doubt and it looks like a question, treat as board-sensitive
        if (text.TrimEnd().EndsWith('?'))
            return VoiceTurnClass.BoardSensitive;

        return VoiceTurnClass.Unclear;
    }
}
