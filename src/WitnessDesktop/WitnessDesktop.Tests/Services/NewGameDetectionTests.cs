using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for auto new-game detection in BrainEventRouter.
/// When the brain detects a starting FEN after previously seeing a non-starting position
/// with meaningful history (>2 entries), a new game is detected and context is reset.
/// </summary>
public class NewGameDetectionTests
{
    private const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string MidGameFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
    private const string LateMidGameFen = "r1bqkb1r/pppppppp/2n2n2/8/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3";

    // ── Detection triggers correctly ──────────────────────────────────────

    [Fact]
    public void StartingFen_AfterNonStartingFen_WithHistory_TriggersNewGame()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        var brainContext = new Mock<IBrainContextService>();
        var frameDiff = new Mock<IFrameDiffService>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            brainContext: brainContext.Object,
            gameJournal: journal,
            frameDiffService: frameDiff.Object,
            onNewGameDetected: summary => receivedSummary = summary);

        // Build history: 3 entries with non-starting FEN
        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));
        journal.AddEntry(MakeEntry(3, LateMidGameFen));

        // Now route an ImageAnalysis result containing starting FEN
        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        // New game should have been detected
        receivedSummary.Should().NotBeNull("new game should be detected");
        journal.EntryCount.Should().BeLessThan(3, "journal should be cleared on new game");
    }

    [Fact]
    public void StartingFen_WithEmptyJournal_DoesNotTrigger()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: summary => receivedSummary = summary);

        // No history, route starting FEN
        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        receivedSummary.Should().BeNull("no history means no new game detection");
    }

    [Fact]
    public void StartingFen_AfterStartingFen_DoesNotTrigger()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: summary => receivedSummary = summary);

        // Build history with starting FEN entries (not a transition)
        journal.AddEntry(MakeEntry(1, StartingFen));
        journal.AddEntry(MakeEntry(2, StartingFen));
        journal.AddEntry(MakeEntry(3, StartingFen));

        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        receivedSummary.Should().BeNull("starting after starting is not a new game");
    }

    [Fact]
    public void NonStartingFen_AfterAnything_DoesNotTrigger()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: summary => receivedSummary = summary);

        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));
        journal.AddEntry(MakeEntry(3, LateMidGameFen));

        // Route non-starting FEN
        var result = MakeImageAnalysisResult($"Position: {MidGameFen}");
        router.RouteBrainResultForTest(result);

        receivedSummary.Should().BeNull("non-starting FEN does not trigger new game");
    }

    [Fact]
    public void StartingFen_WithOnlyTwoEntries_DoesNotTrigger()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: summary => receivedSummary = summary);

        // Only 2 entries -- not enough history
        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));

        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        receivedSummary.Should().BeNull("need >2 entries for meaningful history");
    }

    // ── Side effects on trigger ──────────────────────────────────────────

    [Fact]
    public void OnTrigger_JournalIsCleared()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: _ => { });

        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));
        journal.AddEntry(MakeEntry(3, LateMidGameFen));

        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        // Journal cleared, then the new starting position was added by journal ingestion
        journal.EntryCount.Should().BeLessThanOrEqualTo(1, "journal should be cleared on new game (new entry may be added by ingestion)");
    }

    [Fact]
    public void OnTrigger_FlushEventsIsCalled()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        var brainContext = new Mock<IBrainContextService>();

        var router = new BrainEventRouter(
            timeline.Object,
            brainContext: brainContext.Object,
            gameJournal: journal,
            onNewGameDetected: _ => { });

        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));
        journal.AddEntry(MakeEntry(3, LateMidGameFen));

        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        brainContext.Verify(bc => bc.FlushEvents(), Times.Once);
    }

    [Fact]
    public void OnTrigger_ResetHashIsCalled()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        var frameDiff = new Mock<IFrameDiffService>();

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            frameDiffService: frameDiff.Object,
            onNewGameDetected: _ => { });

        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));
        journal.AddEntry(MakeEntry(3, LateMidGameFen));

        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        frameDiff.Verify(fd => fd.ResetHash(), Times.Once);
    }

    [Fact]
    public void OnTrigger_CallbackInvokedWithSummary()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: summary => receivedSummary = summary);

        journal.AddEntry(MakeEntry(1, MidGameFen, description: "Opening move"));
        journal.AddEntry(MakeEntry(2, LateMidGameFen, description: "Knight development"));
        journal.AddEntry(MakeEntry(3, LateMidGameFen, description: "Center control"));

        var result = MakeImageAnalysisResult($"Position: {StartingFen}");
        router.RouteBrainResultForTest(result);

        receivedSummary.Should().NotBeNullOrEmpty();
        receivedSummary.Should().Contain("3 positions analyzed");
    }

    // ── IsStartingFen edge cases ─────────────────────────────────────────

    [Fact]
    public void IsStartingFen_MatchesStartingPosition()
    {
        // Starting FEN with various move counters should all match
        BrainEventRouter.IsStartingFenForTest(StartingFen).Should().BeTrue();
    }

    [Fact]
    public void IsStartingFen_RejectsNonStartingPosition()
    {
        BrainEventRouter.IsStartingFenForTest(MidGameFen).Should().BeFalse();
        BrainEventRouter.IsStartingFenForTest(LateMidGameFen).Should().BeFalse();
    }

    [Fact]
    public void IsStartingFen_MatchesStartingBoardWithDifferentMoveCounters()
    {
        // Board position is starting, but move counter differs
        var fenWithDifferentCounter = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 5 10";
        BrainEventRouter.IsStartingFenForTest(fenWithDifferentCounter).Should().BeTrue();
    }

    // ── Non-ImageAnalysis results don't trigger ──────────────────────────

    [Fact]
    public void ToolResult_WithStartingFen_DoesNotTriggerNewGame()
    {
        var journal = new GameJournalService();
        var timeline = new Mock<ITimelineFeed>();
        string? receivedSummary = null;

        var router = new BrainEventRouter(
            timeline.Object,
            gameJournal: journal,
            onNewGameDetected: summary => receivedSummary = summary);

        journal.AddEntry(MakeEntry(1, MidGameFen));
        journal.AddEntry(MakeEntry(2, LateMidGameFen));
        journal.AddEntry(MakeEntry(3, LateMidGameFen));

        // ToolResult, not ImageAnalysis
        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = $"Position: {StartingFen}",
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };
        router.RouteBrainResultForTest(result);

        receivedSummary.Should().BeNull("only ImageAnalysis results trigger new game detection");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static GameJournalEntry MakeEntry(int moveNumber, string? fen, string description = "Test position") =>
        new(MoveNumber: moveNumber, Fen: fen, MoveNotation: null, Description: description,
            Evaluation: null, Timestamp: DateTimeOffset.UtcNow);

    private static BrainResult MakeImageAnalysisResult(string analysisText) =>
        new()
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = analysisText,
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
