using GaimerDesktop.Services.Chess;

namespace GaimerDesktop.Tests.Chess;

public class UciParsingTests
{
    // ── Parse Info Lines ──────────────────────────────────────────────────

    [Fact]
    public void ParseInfoLine_CentipawnScore_ExtractsAllFields()
    {
        var line = "info depth 20 seldepth 28 multipv 1 score cp 35 nodes 1234567 nps 4500000 time 274 pv e2e4 e7e5 g1f3 b8c6";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.Depth.Should().Be(20);
        variation.CentipawnEval.Should().Be(35);
        variation.MateIn.Should().BeNull();
        variation.MultiPvIndex.Should().Be(1);
        variation.PrincipalVariation.Should().BeEquivalentTo(new[] { "e2e4", "e7e5", "g1f3", "b8c6" });
        variation.Move.Should().Be("e2e4");
    }

    [Fact]
    public void ParseInfoLine_MateScore_ExtractsMateIn()
    {
        var line = "info depth 25 seldepth 30 multipv 1 score mate 3 nodes 999999 nps 5000000 time 200 pv g5g7 h8g7 d1g4 g7h8 g4g8";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.MateIn.Should().Be(3);
        variation.CentipawnEval.Should().BeNull();
        variation.Move.Should().Be("g5g7");
    }

    [Fact]
    public void ParseInfoLine_NegativeMateScore_ExtractsNegativeMateIn()
    {
        var line = "info depth 18 seldepth 22 multipv 1 score mate -2 nodes 500000 nps 4000000 time 125 pv e1d1 d8d1";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.MateIn.Should().Be(-2);
        variation.CentipawnEval.Should().BeNull();
    }

    [Fact]
    public void ParseInfoLine_NegativeCentipawn_ExtractsCorrectly()
    {
        var line = "info depth 15 seldepth 20 multipv 1 score cp -142 nodes 800000 nps 3500000 time 229 pv d7d5 e4d5";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.CentipawnEval.Should().Be(-142);
    }

    [Fact]
    public void ParseInfoLine_MultiPv2_CorrectIndex()
    {
        var line = "info depth 20 seldepth 25 multipv 2 score cp -15 nodes 2000000 nps 4500000 time 444 pv d2d4 d7d5";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.MultiPvIndex.Should().Be(2);
        variation.Move.Should().Be("d2d4");
    }

    [Fact]
    public void ParseInfoLine_MultiPv3Output_ThreeVariations()
    {
        var lines = new[]
        {
            "info depth 18 seldepth 24 multipv 1 score cp 35 nodes 1000000 nps 4000000 time 250 pv e2e4 e7e5",
            "info depth 18 seldepth 22 multipv 2 score cp 28 nodes 1000000 nps 4000000 time 250 pv d2d4 d7d5",
            "info depth 18 seldepth 21 multipv 3 score cp 18 nodes 1000000 nps 4000000 time 250 pv g1f3 d7d5"
        };

        var variations = lines.Select(l => UciParser.ParseInfoLine(l)).ToList();

        variations.Should().AllSatisfy(v => v.Should().NotBeNull());
        variations[0]!.MultiPvIndex.Should().Be(1);
        variations[1]!.MultiPvIndex.Should().Be(2);
        variations[2]!.MultiPvIndex.Should().Be(3);
        variations[0]!.CentipawnEval.Should().BeGreaterThan(variations[2]!.CentipawnEval!.Value);
    }

    [Fact]
    public void ParseInfoLine_NonInfoLine_ReturnsNull()
    {
        UciParser.ParseInfoLine("readyok").Should().BeNull();
        UciParser.ParseInfoLine("bestmove e2e4").Should().BeNull();
        UciParser.ParseInfoLine("uciok").Should().BeNull();
    }

    [Fact]
    public void ParseInfoLine_InfoWithoutScore_ReturnsNull()
    {
        // Partial info lines (e.g., "info string" or "info currmove") should be ignored
        var line = "info depth 5 currmove e2e4 currmovenumber 1";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().BeNull();
    }

    // ── Parse BestMove Lines ──────────────────────────────────────────────

    [Fact]
    public void ParseBestMove_WithPonder_ExtractsBoth()
    {
        var result = UciParser.ParseBestMove("bestmove e2e4 ponder e7e5");

        result.Should().NotBeNull();
        result!.Value.BestMove.Should().Be("e2e4");
        result.Value.PonderMove.Should().Be("e7e5");
    }

    [Fact]
    public void ParseBestMove_WithoutPonder_ExtractsMoveOnly()
    {
        var result = UciParser.ParseBestMove("bestmove g1f3");

        result.Should().NotBeNull();
        result!.Value.BestMove.Should().Be("g1f3");
        result.Value.PonderMove.Should().BeNull();
    }

    [Fact]
    public void ParseBestMove_NoneMove_Recognized()
    {
        var result = UciParser.ParseBestMove("bestmove (none)");

        result.Should().NotBeNull();
        result!.Value.BestMove.Should().Be("(none)");
        result.Value.PonderMove.Should().BeNull();
    }

    [Fact]
    public void ParseBestMove_NotBestMoveLine_ReturnsNull()
    {
        var result = UciParser.ParseBestMove("info depth 20 score cp 35 pv e2e4");

        result.Should().BeNull();
    }

    // ── ParseNodes / ParseTime ────────────────────────────────────────────

    [Fact]
    public void ParseNodes_ValidInfoLine_ReturnsNodeCount()
    {
        var line = "info depth 20 seldepth 28 multipv 1 score cp 35 nodes 1234567 nps 4500000 time 274 pv e2e4";

        var nodes = UciParser.ParseNodes(line);

        nodes.Should().Be(1234567);
    }

    [Fact]
    public void ParseNodes_NoNodesToken_ReturnsZero()
    {
        var nodes = UciParser.ParseNodes("info depth 5 score cp 10 pv e2e4");

        nodes.Should().Be(0);
    }

    [Fact]
    public void ParseTime_ValidInfoLine_ReturnsTimeMs()
    {
        var line = "info depth 20 seldepth 28 multipv 1 score cp 35 nodes 1234567 nps 4500000 time 274 pv e2e4";

        var time = UciParser.ParseTime(line);

        time.Should().Be(274);
    }

    [Fact]
    public void ParseTime_NoTimeToken_ReturnsZero()
    {
        var time = UciParser.ParseTime("info depth 5 score cp 10 pv e2e4");

        time.Should().Be(0);
    }

    // ── Edge Cases ────────────────────────────────────────────────────────

    [Fact]
    public void ParseInfoLine_ScoreCpWithUpperbound_ExtractsCp()
    {
        // Stockfish sometimes emits "score cp 35 upperbound" during aspiration window search
        var line = "info depth 20 multipv 1 score cp 35 upperbound nodes 500000 time 100 pv e2e4";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.CentipawnEval.Should().Be(35);
    }

    [Fact]
    public void ParseInfoLine_ScoreCpWithLowerbound_ExtractsCp()
    {
        var line = "info depth 20 multipv 1 score cp -50 lowerbound nodes 500000 time 100 pv d7d5";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.CentipawnEval.Should().Be(-50);
    }

    [Fact]
    public void ParseInfoLine_InfoStringLine_ReturnsNull()
    {
        // "info string" lines are diagnostic messages, not analysis
        var line = "info string NNUE evaluation using nn-...";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().BeNull("info string lines have no score data");
    }

    [Fact]
    public void ParseInfoLine_ScoreButNoPv_ReturnsVariationWithEmptyMove()
    {
        // Rare but possible: score without PV (e.g., hash hit at low depth)
        var line = "info depth 1 multipv 1 score cp 0 nodes 100 time 1";

        var variation = UciParser.ParseInfoLine(line);

        variation.Should().NotBeNull();
        variation!.CentipawnEval.Should().Be(0);
        variation.Move.Should().BeEmpty("no PV means no best move extracted");
        variation.PrincipalVariation.Should().BeEmpty();
    }

    [Fact]
    public void ParseInfoLine_EmptyString_ReturnsNull()
    {
        UciParser.ParseInfoLine("").Should().BeNull();
        UciParser.ParseInfoLine("   ").Should().BeNull();
    }

    [Fact]
    public void ParseBestMove_EmptyString_ReturnsNull()
    {
        UciParser.ParseBestMove("").Should().BeNull();
        UciParser.ParseBestMove("   ").Should().BeNull();
    }

    [Fact]
    public void ParseBestMove_PromotionMove_Recognized()
    {
        // Promotion move is 5 chars: a7a8q
        var result = UciParser.ParseBestMove("bestmove a7a8q ponder b8c6");

        result.Should().NotBeNull();
        result!.Value.BestMove.Should().Be("a7a8q");
        result.Value.PonderMove.Should().Be("b8c6");
    }

    // ── Build EngineAnalysis from Parsed Data ─────────────────────────────

    [Fact]
    public void BuildAnalysis_FromVariationsAndBestMove_Correct()
    {
        var variations = new List<EngineVariation>
        {
            new(MultiPvIndex: 1, Move: "e2e4", CentipawnEval: 35, MateIn: null,
                PrincipalVariation: new[] { "e2e4", "e7e5", "g1f3" }, Depth: 20),
            new(MultiPvIndex: 2, Move: "d2d4", CentipawnEval: 28, MateIn: null,
                PrincipalVariation: new[] { "d2d4", "d7d5" }, Depth: 20),
        };

        var analysis = new EngineAnalysis(
            BestMove: "e2e4",
            PonderMove: "e7e5",
            Depth: 20,
            Variations: variations,
            Nodes: 1234567,
            TimeMs: 274);

        analysis.BestMove.Should().Be("e2e4");
        analysis.PonderMove.Should().Be("e7e5");
        analysis.Depth.Should().Be(20);
        analysis.Variations.Should().HaveCount(2);
        analysis.Variations[0].CentipawnEval.Should().Be(35);
        analysis.Nodes.Should().Be(1234567);
    }
}
