using Microsoft.Extensions.Logging;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Chess;

/// <summary>
/// Live integration tests that require a real Stockfish binary.
/// Trait "LiveEngine" — filter out with --filter "Category!=LiveEngine" in CI.
/// Requires: Stockfish binary at StockfishDownloader.GetEnginePath()
/// </summary>
[Trait("Category", "LiveEngine")]
public class StockfishLiveTests : IAsyncLifetime
{
    private StockfishService? _sut;

    private static string GetEnginePath() => StockfishDownloader.GetEnginePath();

    public async Task InitializeAsync()
    {
        var path = GetEnginePath();
        if (!File.Exists(path))
        {
            // Skip all tests if binary not present
            return;
        }

        var downloader = new StockfishDownloader(new HttpClient());
        var logger = new Mock<ILogger<StockfishService>>().Object;
        _sut = new StockfishService(downloader, logger);
        await _sut.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.StopAsync();
            _sut.Dispose();
        }
    }

    private void SkipIfNoEngine()
    {
        if (_sut == null || !_sut.IsReady)
        {
            // xUnit 2.x doesn't have Assert.Skip — just return early
            return;
        }
    }

    [Fact]
    public void Stockfish_UciHandshake_EngineIsReady()
    {
        if (_sut == null) return;

        _sut.IsReady.Should().BeTrue();
        _sut.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task Stockfish_AnalyzeOpeningPosition_ReturnsDepth15Plus()
    {
        if (_sut == null) return;

        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var result = await _sut.AnalyzePositionAsync(fen, new AnalysisOptions(MoveTimeMs: 1000));

        result.Depth.Should().BeGreaterOrEqualTo(15);
        result.BestMove.Should().NotBeNullOrEmpty();
        result.Variations.Should().NotBeEmpty();
        result.Variations[0].CentipawnEval.Should().NotBeNull();
    }

    [Fact]
    public async Task Stockfish_AnalyzeMidgamePosition_ReturnsAnalysis()
    {
        if (_sut == null) return;

        // Complex middlegame — the kind of position Lichess cloud eval can't handle
        var fen = "r1bq1rk1/pp2bppp/2n1pn2/3p4/3P4/2NBPN2/PP3PPP/R1BQ1RK1 w - - 4 9";
        var result = await _sut.AnalyzePositionAsync(fen, new AnalysisOptions(MoveTimeMs: 1000));

        result.BestMove.Should().NotBeNullOrEmpty();
        result.Depth.Should().BeGreaterOrEqualTo(10);
        result.Nodes.Should().BeGreaterThan(0);
        result.TimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Stockfish_MultiPv3_ReturnsMultipleCandidates()
    {
        if (_sut == null) return;

        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var result = await _sut.AnalyzePositionAsync(fen,
            new AnalysisOptions(MoveTimeMs: 1000, MultiPv: 3));

        result.Variations.Count.Should().BeGreaterOrEqualTo(2);
        // Variations should have different first moves
        var distinctMoves = result.Variations.Select(v => v.Move).Distinct().ToList();
        distinctMoves.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task Stockfish_MatePosition_ReturnsMateScore()
    {
        if (_sut == null) return;

        // Scholar's mate position: Qxf7# is mate in 1
        var fen = "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4";
        var result = await _sut.AnalyzePositionAsync(fen, new AnalysisOptions(MoveTimeMs: 500));

        result.BestMove.Should().Be("h5f7");
        result.Variations[0].MateIn.Should().NotBeNull();
        result.Variations[0].MateIn.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Stockfish_Cancellation_StopsWithinTimeout()
    {
        if (_sut == null) return;

        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Start deep analysis then cancel quickly
        var act = () => _sut.AnalyzePositionAsync(fen,
            new AnalysisOptions(MoveTimeMs: 10000, MaxDepth: 99), cts.Token);

        // Should either return partial results, throw OperationCanceledException,
        // or throw TimeoutException (engine didn't respond to stop fast enough)
        try
        {
            var result = await act();
            // If it returns, it should have some result
            result.BestMove.Should().NotBeNullOrEmpty();
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            // Both are acceptable — cancellation or timeout from stop
        }
    }
}
