using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for BrainPromptBuilder — assembles structured system and user prompts
/// for the brain image analysis pipeline. Validates that identity, capabilities,
/// output format, game context, connection state, and previous game sections
/// are correctly assembled.
/// </summary>
public class BrainPromptBuilderTests
{
    private static readonly GameSkillPack _chessPack = new()
    {
        Id = "chess-online",
        Genre = "chess",
        BrainInstructionsContent = @"[CAPABILITIES]
You receive a view of the chess board whenever it changes — you're watching in near real-time.
You can request a fresh view anytime using capture_screen.
You have Stockfish for engine analysis via analyze_position_engine (provide FEN).
You can review game history via game_journal.
You can look up chess knowledge via web_search.

[OUTPUT FORMAT]
Follow this two-step process for every board image:

Step 1 — VISUAL DESCRIPTION (visual_description field):
Describe exactly what you see on the board. List piece positions, colors, and any text/clocks visible.
Do NOT interpret or assess — just record raw observations.

Step 2 — ANALYSIS (position_assessment, threats, suggested_action fields):
Based on your visual description above, provide:
1. Position assessment (who is better and why, 1-2 sentences)
2. Key threat or opportunity (if any)
3. Recommended action or plan (1-2 sentences)

FEN EXTRACTION (fen field):
IMPORTANT: End your response with the FEN string in this exact format:
[FEN: {fen_string}]
Extract the FEN carefully from the board position. This is used for game tracking.

If a critical tactical opportunity or blunder exists, call analyze_position_engine with the FEN BEFORE responding.

CONFIDENCE CALIBRATION (confidence field):
Tag your overall assessment with one of these confidence levels:
- CERTAIN (>95%): Board is crystal clear, all pieces unambiguous
- LIKELY (75-95%): Most pieces clear, minor ambiguity on 1-2 squares
- UNCERTAIN (50-75%): Several pieces hard to distinguish, partial read
- GUESSING (<50%): Board is largely unreadable, low confidence in position

[READING ACCURACY]
If a piece or position cannot be clearly identified from the image, output UNREADABLE for that square.
A partial reading is better than an incorrect one. For example:
  ""e4: white pawn, d5: UNREADABLE, f6: black knight""
Never guess a piece identity when the image is ambiguous — use UNREADABLE instead.",
        UserPromptTemplate = "Current game: chess. Position #{moveNumber}."
    };

    private class TestPackService : IGameSkillPackService
    {
        public GameSkillPack? ActivePack { get; }
        public TestPackService(GameSkillPack? pack) => ActivePack = pack;
        public GameSkillPack? LoadPack(string packId) => ActivePack;
        public void SetActivePack(string? packId) { }
        public IReadOnlyList<string> GetAvailablePackIds() => Array.Empty<string>();
    }

    private static BrainPromptBuilder MakeChessBuilder() => new(new TestPackService(_chessPack));

    private static Agent MakeAgent(string? brainPrefix = null) => new()
    {
        Key = "test-chess",
        Id = "Chess Master",
        Name = "TestAgent",
        PrimaryGame = "Chess",
        IconImage = "icon.png",
        PortraitImage = "portrait.png",
        Description = "Test chess agent",
        Features = ["Analysis"],
        SystemInstruction = "Legacy instruction",
        Type = AgentType.Chess,
        BrainPersonalityPrefix = brainPrefix ?? "[IDENTITY]\nYou are TestAgent, a chess analyst.\n\n[VISION]\nYou watch the board.",
    };

    private static BrainEvent MakeEvent(
        string category = "observation",
        string text = "Pawn moved to e4",
        DateTime? timestamp = null) => new()
    {
        TimestampUtc = timestamp ?? new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc),
        Type = BrainEventType.VisionObservation,
        Category = category,
        Text = text,
        Confidence = 0.9,
    };

    // ── BuildSystemPrompt: Identity section ──────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesAgentPersonalityPrefix()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent("[IDENTITY]\nYou are Leroy, chess genius.");

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("You are Leroy, chess genius.");
    }

    // ── BuildSystemPrompt: Capabilities section ──────────────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesCapabilitiesSection()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("capture_screen");
        result.Should().Contain("analyze_position_engine");
        result.Should().Contain("game_journal");
    }

    // ── BuildSystemPrompt: Output format section ─────────────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesOutputFormatWithFenInstruction()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("[FEN:");
        result.Should().Contain("Position assessment");
        result.Should().Contain("analyze_position_engine");
    }

    // ── BuildSystemPrompt: Game Context — Journal ────────────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesJournalSummary_WhenProvided()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], "Game: 5 positions analyzed. Opening e4.", null, null, true);

        result.Should().Contain("Game Journal");
        result.Should().Contain("5 positions analyzed");
    }

    [Fact]
    public void BuildSystemPrompt_ShowsDefaultText_WhenJournalNull()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("No positions recorded yet.");
    }

    // ── BuildSystemPrompt: Game Context — L1 Events ──────────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesL1Events_FormattedWithTimestamps()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();
        var events = new List<BrainEvent>
        {
            MakeEvent("threat", "Knight fork on f7", new DateTime(2026, 3, 10, 14, 30, 15, DateTimeKind.Utc)),
            MakeEvent("observation", "Pawn moved to e4", new DateTime(2026, 3, 10, 14, 30, 10, DateTimeKind.Utc)),
        };

        var result = sut.BuildSystemPrompt(agent, events, null, null, null, true);

        result.Should().Contain("Recent Observations");
        result.Should().Contain("14:30:15");
        result.Should().Contain("threat: Knight fork on f7");
        result.Should().Contain("observation: Pawn moved to e4");
    }

    [Fact]
    public void BuildSystemPrompt_LimitsL1EventsToMax5()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();
        var events = Enumerable.Range(0, 10)
            .Select(i => MakeEvent(text: $"Event {i}"))
            .ToList();

        var result = sut.BuildSystemPrompt(agent, events, null, null, null, true);

        // Should contain only 5 events
        var eventCount = Enumerable.Range(0, 10).Count(i => result.Contains($"Event {i}"));
        eventCount.Should().Be(5);
    }

    [Fact]
    public void BuildSystemPrompt_WithEmptyL1Events_ShowsNoRecentObservations()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("No recent observations.");
    }

    // ── BuildSystemPrompt: Game Context — Rolling Summary ────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesRollingSummary_WhenProvided()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, "Player has been developing pieces on the kingside.", null, true);

        result.Should().Contain("Rolling Summary");
        result.Should().Contain("developing pieces on the kingside");
    }

    [Fact]
    public void BuildSystemPrompt_ShowsDefaultText_WhenRollingSummaryNull()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("No rolling summary available.");
    }

    // ── BuildSystemPrompt: Previous Game ─────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_IncludesPreviousGameSummary_WhenProvided()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, "Lost as white in Sicilian after 40 moves.", true);

        result.Should().Contain("Previous Game");
        result.Should().Contain("Lost as white in Sicilian");
    }

    [Fact]
    public void BuildSystemPrompt_OmitsPreviousGameSection_WhenNull()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().NotContain("Previous Game");
    }

    // ── BuildSystemPrompt: Connection state awareness ────────────────────────

    [Fact]
    public void BuildSystemPrompt_WhenConnected_DoesNotIncludeConnectionGuidance()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, isConnectedToGame: true);

        result.Should().NotContain("[CONNECTION STATUS]");
    }

    [Fact]
    public void BuildSystemPrompt_WhenNotConnected_IncludesConnectionAwareness()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, isConnectedToGame: false);

        result.Should().Contain("[CONNECTION STATUS]");
        result.Should().Contain("not currently connected");
    }

    [Fact]
    public void BuildSystemPrompt_WhenNotConnected_GuidesToConnectNaturally()
    {
        var sut = new BrainPromptBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, isConnectedToGame: false);

        // Should encourage connecting but not as a hard rule
        result.Should().Contain("connect");
        result.Should().NotContain("You must connect");
    }

    // ── BuildUserPrompt ──────────────────────────────────────────────────────

    [Fact]
    public void BuildUserPrompt_ReturnsExpectedFormat()
    {
        var sut = MakeChessBuilder();

        var result = sut.BuildUserPrompt("chess", 5);

        result.Should().Be("Current game: chess. Position #5.");
    }

    [Fact]
    public void BuildUserPrompt_HandlesZeroMoveNumber()
    {
        var sut = MakeChessBuilder();

        var result = sut.BuildUserPrompt("chess", 0);

        result.Should().Contain("Position #0");
    }

    // ── Full system prompt structure ─────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_SectionsAppearInCorrectOrder()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();
        var events = new List<BrainEvent> { MakeEvent() };

        var result = sut.BuildSystemPrompt(agent, events, "Journal text", "Rolling text", "Previous game text", true);

        var identityIdx = result.IndexOf("[IDENTITY]");
        var capIdx = result.IndexOf("[CAPABILITIES]");
        var outputIdx = result.IndexOf("[OUTPUT FORMAT]");
        var journalIdx = result.IndexOf("Game Journal");
        var recentIdx = result.IndexOf("Recent Observations");
        var rollingIdx = result.IndexOf("Rolling Summary");
        var prevIdx = result.IndexOf("Previous Game");

        // All sections present
        identityIdx.Should().BeGreaterOrEqualTo(0);
        capIdx.Should().BeGreaterThan(identityIdx);
        outputIdx.Should().BeGreaterThan(capIdx);
        journalIdx.Should().BeGreaterThan(outputIdx);
        recentIdx.Should().BeGreaterThan(journalIdx);
        rollingIdx.Should().BeGreaterThan(recentIdx);
        prevIdx.Should().BeGreaterThan(rollingIdx);
    }

    // ── Phase 16: Chain-of-thought, UNREADABLE, confidence calibration ──────

    [Fact]
    public void BuildSystemPrompt_IncludesChainOfThoughtInstruction()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        // Should instruct two-step process: visual description first, then analysis
        result.Should().Contain("visual_description");
        result.Should().Contain("Describe exactly what you see");
    }

    [Fact]
    public void BuildSystemPrompt_IncludesUnreadableEscapeHatch()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("UNREADABLE");
        result.Should().Contain("[READING ACCURACY]");
    }

    [Fact]
    public void BuildSystemPrompt_IncludesConfidenceCalibrationTags()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        result.Should().Contain("CERTAIN");
        result.Should().Contain(">95%");
        result.Should().Contain("LIKELY");
        result.Should().Contain("75-95%");
        result.Should().Contain("UNCERTAIN");
        result.Should().Contain("50-75%");
        result.Should().Contain("GUESSING");
        result.Should().Contain("<50%");
    }

    [Fact]
    public void BuildSystemPrompt_OutputFormatIncludesVisualDescriptionStep()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();

        var result = sut.BuildSystemPrompt(agent, [], null, null, null, true);

        // visual_description should appear as Step 1 in OUTPUT FORMAT, before analysis fields
        var outputIdx = result.IndexOf("[OUTPUT FORMAT]");
        var visualDescIdx = result.IndexOf("visual_description", outputIdx);
        var positionAssessIdx = result.IndexOf("Position assessment", outputIdx);

        outputIdx.Should().BeGreaterOrEqualTo(0);
        visualDescIdx.Should().BeGreaterThan(outputIdx);
        positionAssessIdx.Should().BeGreaterThan(visualDescIdx);
    }

    [Fact]
    public void BuildSystemPrompt_SectionsAppearInCorrectOrder_WithNewSections()
    {
        var sut = MakeChessBuilder();
        var agent = MakeAgent();
        var events = new List<BrainEvent> { MakeEvent() };

        var result = sut.BuildSystemPrompt(agent, events, "Journal text", "Rolling text", "Previous game text", true);

        var outputIdx = result.IndexOf("[OUTPUT FORMAT]");
        var readingIdx = result.IndexOf("[READING ACCURACY]");
        var journalIdx = result.IndexOf("Game Journal");

        // [OUTPUT FORMAT] -> [READING ACCURACY] -> Game Context sections
        outputIdx.Should().BeGreaterOrEqualTo(0);
        readingIdx.Should().BeGreaterThan(outputIdx);
        journalIdx.Should().BeGreaterThan(readingIdx);
    }

    // ── Interface conformance ────────────────────────────────────────────────

    [Fact]
    public void BrainPromptBuilder_ImplementsIBrainPromptBuilder()
    {
        var sut = new BrainPromptBuilder();

        sut.Should().BeAssignableTo<IBrainPromptBuilder>();
    }
}
