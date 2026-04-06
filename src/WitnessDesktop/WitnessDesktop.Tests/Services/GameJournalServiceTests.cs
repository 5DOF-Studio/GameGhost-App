using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for GameJournalService — in-memory move-by-move game journal.
/// Validates entry tracking, FEN retrieval, summary generation, cap enforcement,
/// thread safety, and telemetry integration.
/// </summary>
public class GameJournalServiceTests
{
    private static GameJournalEntry MakeEntry(
        int moveNumber = 1,
        string? fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
        string? moveNotation = null,
        string description = "Test position",
        string? evaluation = null,
        DateTimeOffset? timestamp = null) => new(
        MoveNumber: moveNumber,
        Fen: fen,
        MoveNotation: moveNotation,
        Description: description,
        Evaluation: evaluation,
        Timestamp: timestamp ?? DateTimeOffset.UtcNow);

    // ── AddEntry / EntryCount ────────────────────────────────────────────────

    [Fact]
    public void AddEntry_IncreasesEntryCount()
    {
        var sut = new GameJournalService();

        sut.AddEntry(MakeEntry());

        sut.EntryCount.Should().Be(1);
    }

    [Fact]
    public void AddEntry_MultipleTimes_TracksAll()
    {
        var sut = new GameJournalService();

        sut.AddEntry(MakeEntry(moveNumber: 1));
        sut.AddEntry(MakeEntry(moveNumber: 2));
        sut.AddEntry(MakeEntry(moveNumber: 3));

        sut.EntryCount.Should().Be(3);
    }

    // ── GetEntries ───────────────────────────────────────────────────────────

    [Fact]
    public void GetEntries_ReturnsAllEntriesInOrder()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1, description: "First"));
        sut.AddEntry(MakeEntry(moveNumber: 2, description: "Second"));
        sut.AddEntry(MakeEntry(moveNumber: 3, description: "Third"));

        var entries = sut.GetEntries();

        entries.Should().HaveCount(3);
        entries[0].Description.Should().Be("First");
        entries[1].Description.Should().Be("Second");
        entries[2].Description.Should().Be("Third");
    }

    [Fact]
    public void GetEntries_ReturnsSnapshotCopy()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1));

        var snapshot1 = sut.GetEntries();
        sut.AddEntry(MakeEntry(moveNumber: 2));
        var snapshot2 = sut.GetEntries();

        // First snapshot should not be affected by subsequent adds
        snapshot1.Should().HaveCount(1);
        snapshot2.Should().HaveCount(2);
    }

    // ── GetLatestFen ─────────────────────────────────────────────────────────

    [Fact]
    public void GetLatestFen_ReturnsLastEntryFen()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1, fen: "fen1"));
        sut.AddEntry(MakeEntry(moveNumber: 2, fen: "fen2"));

        sut.GetLatestFen().Should().Be("fen2");
    }

    [Fact]
    public void GetLatestFen_ReturnsNull_WhenEmpty()
    {
        var sut = new GameJournalService();

        sut.GetLatestFen().Should().BeNull();
    }

    [Fact]
    public void GetLatestFen_ReturnsNull_WhenLastEntryHasNullFen()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1, fen: "fen1"));
        sut.AddEntry(MakeEntry(moveNumber: 2, fen: null));

        sut.GetLatestFen().Should().BeNull();
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsToEmpty()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1));
        sut.AddEntry(MakeEntry(moveNumber: 2));

        sut.Clear();

        sut.EntryCount.Should().Be(0);
        sut.GetEntries().Should().BeEmpty();
        sut.GetLatestFen().Should().BeNull();
    }

    // ── GetSummary ───────────────────────────────────────────────────────────

    [Fact]
    public void GetSummary_ReturnsEmptyMessage_WhenNoEntries()
    {
        var sut = new GameJournalService();

        sut.GetSummary().Should().Be("No positions recorded yet.");
    }

    [Fact]
    public void GetSummary_ReturnsMeaningfulText_WithEntries()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1, fen: "startFen", description: "Opening position"));
        sut.AddEntry(MakeEntry(moveNumber: 2, description: "Pawn to e4"));

        var summary = sut.GetSummary();

        summary.Should().Contain("2 positions analyzed");
        summary.Should().Contain("Pawn to e4");
        summary.Should().Contain("startFen");
    }

    // ── Cap at 200 ───────────────────────────────────────────────────────────

    [Fact]
    public void AddEntry_CapsAt200_OldestDropped()
    {
        var sut = new GameJournalService();

        for (int i = 1; i <= 201; i++)
        {
            sut.AddEntry(MakeEntry(moveNumber: i, description: $"Move {i}"));
        }

        sut.EntryCount.Should().Be(200);
        // First entry should be dropped (Move 1), so the oldest should be Move 2
        var entries = sut.GetEntries();
        entries[0].Description.Should().Be("Move 2");
        entries[^1].Description.Should().Be("Move 201");
    }

    // ── Thread safety ────────────────────────────────────────────────────────

    [Fact]
    public void ConcurrentAddAndGet_DoesNotThrow()
    {
        var sut = new GameJournalService();

        var act = () =>
        {
            Parallel.For(0, 100, i =>
            {
                sut.AddEntry(MakeEntry(moveNumber: i, description: $"Move {i}"));
                _ = sut.GetEntries();
                _ = sut.EntryCount;
                _ = sut.GetLatestFen();
                _ = sut.GetSummary();
            });
        };

        act.Should().NotThrow();
    }

    // ── Telemetry integration ────────────────────────────────────────────────

    [Fact]
    public void AddEntry_WithTelemetry_TracksEvent()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var sut = new GameJournalService(mockTelemetry.Object);

        sut.AddEntry(MakeEntry());

        mockTelemetry.Verify(t => t.TrackEvent("journal", "entry_added",
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public void AddEntry_WithoutTelemetry_DoesNotThrow()
    {
        var sut = new GameJournalService();

        var act = () => sut.AddEntry(MakeEntry());

        act.Should().NotThrow();
    }

    // ── Temporal Consistency Validation ──────────────────────────────────────

    [Fact]
    public void ValidateTemporalConsistency_FirstEntry_ReturnsConsistent()
    {
        var sut = new GameJournalService();

        var result = sut.ValidateTemporalConsistency("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        result.IsConsistent.Should().BeTrue();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void ValidateTemporalConsistency_NullFen_ReturnsConsistent()
    {
        var sut = new GameJournalService();
        sut.AddEntry(MakeEntry(moveNumber: 1));

        var result = sut.ValidateTemporalConsistency(null);

        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public void ValidateTemporalConsistency_DuplicateFen_ReturnsDuplicate()
    {
        var sut = new GameJournalService();
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        sut.AddEntry(MakeEntry(moveNumber: 1, fen: fen));

        var result = sut.ValidateTemporalConsistency(fen);

        result.IsConsistent.Should().BeFalse();
        result.Warning.Should().Be("DUPLICATE_POSITION");
    }

    [Fact]
    public void ValidateTemporalConsistency_NormalMove_ReturnsConsistent()
    {
        var sut = new GameJournalService();
        // Starting position: 32 pieces
        sut.AddEntry(MakeEntry(moveNumber: 1,
            fen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"));

        // After e4: 32 pieces (just moved, no capture)
        var result = sut.ValidateTemporalConsistency(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public void ValidateTemporalConsistency_CaptureMove_ReturnsConsistent()
    {
        var sut = new GameJournalService();
        // 32 pieces
        sut.AddEntry(MakeEntry(moveNumber: 1,
            fen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"));

        // 31 pieces (1 captured) — valid, diff = 1
        var result = sut.ValidateTemporalConsistency(
            "rnbqkbnr/ppppppp1/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public void ValidateTemporalConsistency_ImpossibleTransition_ReturnsInconsistent()
    {
        var sut = new GameJournalService();
        // 32 pieces (full board)
        sut.AddEntry(MakeEntry(moveNumber: 1,
            fen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"));

        // 24 pieces — 8 piece difference is impossible in one move
        var result = sut.ValidateTemporalConsistency(
            "4kbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        result.IsConsistent.Should().BeFalse();
        result.Warning.Should().Be("IMPOSSIBLE_TRANSITION");
    }

    // ── Interface conformance ────────────────────────────────────────────────

    [Fact]
    public void GameJournalService_ImplementsIGameJournalService()
    {
        var sut = new GameJournalService();

        sut.Should().BeAssignableTo<IGameJournalService>();
    }
}
