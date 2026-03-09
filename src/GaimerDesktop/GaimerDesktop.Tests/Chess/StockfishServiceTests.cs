using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Chess;

public class StockfishServiceTests
{
    private const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    // ── MockStockfishService ──────────────────────────────────────────────

    [Fact]
    public void Mock_IsInstalled_ReturnsTrue()
    {
        using var sut = new MockStockfishService();

        sut.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task Mock_EnsureInstalled_ReportsProgressAndReturnsTrue()
    {
        using var sut = new MockStockfishService();
        var progressValues = new List<double>();

        // Use direct IProgress<double> implementation to avoid SynchronizationContext issues
        var progress = new DirectProgress<double>(v => progressValues.Add(v));

        var result = await sut.EnsureInstalledAsync(progress);

        result.Should().BeTrue();
        progressValues.Should().Contain(1.0, "MockStockfishService should report 100% progress");
    }

    [Fact]
    public async Task Mock_StartAsync_SetsIsReady()
    {
        using var sut = new MockStockfishService();

        sut.IsReady.Should().BeFalse();
        await sut.StartAsync();
        sut.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task Mock_StopAsync_ClearsIsReady()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();

        await sut.StopAsync();

        sut.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task Mock_AnalyzePosition_StartingPosition_ReturnsCannedResult()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();

        var result = await sut.AnalyzePositionAsync(StartingFen);

        result.BestMove.Should().Be("e2e4");
        result.PonderMove.Should().Be("e7e5");
        result.Depth.Should().BeGreaterThan(0);
        result.Variations.Should().HaveCountGreaterThan(0);
        result.Variations[0].CentipawnEval.Should().Be(30);
    }

    [Fact]
    public async Task Mock_AnalyzePosition_UnknownFen_ReturnsFallback()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();

        // Some random valid position not in the canned results
        var result = await sut.AnalyzePositionAsync(
            "r1bqkbnr/pppppppp/2n5/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 1 2");

        result.BestMove.Should().NotBeNullOrEmpty();
        result.Variations.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Mock_AnalyzePosition_MatePosition_ReturnsMateIn()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();

        var result = await sut.AnalyzePositionAsync(
            "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4");

        result.BestMove.Should().Be("h5f7");
        result.Variations[0].MateIn.Should().Be(1);
        result.Variations[0].CentipawnEval.Should().BeNull();
    }

    [Fact]
    public async Task Mock_AnalyzePosition_InvalidFen_ThrowsArgumentException()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();

        var act = () => sut.AnalyzePositionAsync("not a valid fen string");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid FEN*");
    }

    [Fact]
    public async Task Mock_AnalyzePosition_BeforeStart_ThrowsInvalidOperation()
    {
        using var sut = new MockStockfishService();

        var act = () => sut.AnalyzePositionAsync(StartingFen);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not ready*");
    }

    [Fact]
    public async Task Mock_AnalyzePosition_Cancelled_ThrowsOperationCanceled()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.AnalyzePositionAsync(StartingFen, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Mock_AnalyzePosition_WithOptions_DoesNotThrow()
    {
        using var sut = new MockStockfishService();
        await sut.StartAsync();

        var options = new AnalysisOptions(MoveTimeMs: 2000, MaxDepth: 30, MultiPv: 3);
        var result = await sut.AnalyzePositionAsync(StartingFen, options);

        result.Should().NotBeNull();
        result.BestMove.Should().NotBeNullOrEmpty();
    }

    // ── Model Records ─────────────────────────────────────────────────────

    [Fact]
    public void AnalysisOptions_Defaults_AreReasonable()
    {
        var defaults = new AnalysisOptions();

        defaults.MoveTimeMs.Should().Be(1000);
        defaults.MaxDepth.Should().Be(25);
        defaults.MultiPv.Should().Be(1);
    }

    [Fact]
    public void EngineVariation_PrimitiveFields_MatchAcrossInstances()
    {
        var pv = new[] { "e2e4", "e7e5" };
        var v1 = new EngineVariation(1, "e2e4", 35, null, pv, 20);
        var v2 = new EngineVariation(1, "e2e4", 35, null, pv, 20);

        // Record value equality for primitives; arrays compare by reference
        v1.Move.Should().Be(v2.Move);
        v1.CentipawnEval.Should().Be(v2.CentipawnEval);
        v1.MultiPvIndex.Should().Be(v2.MultiPvIndex);
        v1.MateIn.Should().Be(v2.MateIn);
        v1.Depth.Should().Be(v2.Depth);
        // Same array ref → record Equals returns true
        v1.Should().Be(v2);
    }

    [Fact]
    public void EngineVariation_DifferentArrayRefs_NotEqualByRecord()
    {
        var v1 = new EngineVariation(1, "e2e4", 35, null, new[] { "e2e4", "e7e5" }, 20);
        var v2 = new EngineVariation(1, "e2e4", 35, null, new[] { "e2e4", "e7e5" }, 20);

        // Different array instances → record Equals is false (arrays use reference equality)
        v1.Should().NotBe(v2, "record equality on arrays compares by reference, not contents");
        // But all primitive fields still match
        v1.Move.Should().Be(v2.Move);
        v1.CentipawnEval.Should().Be(v2.CentipawnEval);
    }

    [Fact]
    public void EngineAnalysis_RecordCreation_Works()
    {
        var variations = new List<EngineVariation>
        {
            new(1, "e2e4", 35, null, new[] { "e2e4" }, 20)
        }.AsReadOnly();

        var analysis = new EngineAnalysis("e2e4", "e7e5", 20, variations, 1_500_000, 350);

        analysis.BestMove.Should().Be("e2e4");
        analysis.PonderMove.Should().Be("e7e5");
        analysis.Depth.Should().Be(20);
        analysis.Variations.Should().HaveCount(1);
        analysis.Nodes.Should().Be(1_500_000);
        analysis.TimeMs.Should().Be(350);
    }

    // ── StockfishDownloader ───────────────────────────────────────────────

    [Fact]
    public void GetEnginePath_ReturnsNonEmpty()
    {
        var path = StockfishDownloader.GetEnginePath();

        path.Should().NotBeNullOrEmpty();
        path.Should().Contain("engines");
        path.Should().Contain("stockfish");
    }

    [Fact]
    public void GetEnginePath_NotUnderDocuments()
    {
        // Regression: SpecialFolder.ApplicationData resolved to ~/Documents/.config/ on
        // Mac Catalyst, which has File Provider extended attributes blocking Process.Start().
        var path = StockfishDownloader.GetEnginePath();

        path.Should().NotContain("Documents");
        path.Should().NotContain(".config");
    }

    [Fact]
    public void GetEnginePath_UnderApplicationSupport_OnMac()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX))
            return;

        var path = StockfishDownloader.GetEnginePath();
        path.Should().Contain("Application Support");
    }

    [Fact]
    public void GetAssetName_ReturnsPlatformSpecific()
    {
        var name = StockfishDownloader.GetAssetName();

        name.Should().NotBeNullOrEmpty();
        name.Should().Contain("stockfish");
        // Should end with .tar or .zip
        (name.EndsWith(".tar") || name.EndsWith(".zip")).Should().BeTrue();
    }

    [Fact]
    public async Task Downloader_BadUrl_ReturnsFalse()
    {
        var handler = new MockHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)));
        var httpClient = new HttpClient(handler);
        var downloader = new StockfishDownloader(httpClient);

        var result = await downloader.DownloadAsync("https://example.com/nonexistent.tar");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Downloader_ReportsProgress()
    {
        var content = new byte[1024];
        var handler = new MockHttpHandler((_, _) =>
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentLength = content.Length;
            return Task.FromResult(response);
        });
        var httpClient = new HttpClient(handler);
        var downloader = new StockfishDownloader(httpClient);

        var progressValues = new List<double>();
        var progress = new DirectProgress<double>(v => progressValues.Add(v));

        // Download will fail at SHA verification (dummy bytes), but progress should still be reported
        var result = await downloader.DownloadAsync(
            "https://example.com/stockfish.tar",
            expectedSha256: "0000000000000000000000000000000000000000000000000000000000000000",
            progress: progress);

        result.Should().BeFalse("SHA256 mismatch expected for dummy bytes");
        progressValues.Should().NotBeEmpty("progress should be reported during download");
        progressValues.Should().OnlyContain(v => v >= 0.0 && v <= 1.0, "progress values should be 0.0-1.0");
    }

    [Fact]
    public async Task Downloader_Cancellation_ThrowsOrReturnsFalse()
    {
        var handler = new MockHttpHandler(async (_, _) =>
        {
            await Task.Delay(5000); // simulate slow download
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var downloader = new StockfishDownloader(httpClient);

        using var cts = new CancellationTokenSource(50);

        Func<Task> act = () => downloader.DownloadAsync(
            "https://example.com/stockfish.tar",
            ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
