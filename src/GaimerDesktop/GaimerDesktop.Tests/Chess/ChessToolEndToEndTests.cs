using System.Net;
using Microsoft.Extensions.Logging;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Chess;

/// <summary>
/// End-to-end tests for chess analysis tool pipeline:
/// FEN validation -> ToolExecutor dispatch -> MockStockfishService -> formatted JSON result.
/// Uses MockStockfishService (no real engine binary needed).
/// </summary>
public class ChessToolEndToEndTests
{
    private readonly Mock<IWindowCaptureService> _mockCapture = new();
    private readonly Mock<ISessionManager> _mockSession = new();
    private readonly Mock<ILogger<ToolExecutor>> _mockLogger = new();
    private readonly MockStockfishService _stockfish = new();

    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _lichessHandler;
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _openRouterHandler;

    public ChessToolEndToEndTests()
    {
        _mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-chess-game",
            GameType = "chess",
            ConnectorName = "lichess"
        });

        _lichessHandler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
        _openRouterHandler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"choices":[{"message":{"content":"Strategic analysis: solid position with pawn center."},"finish_reason":"stop"}]}""",
                System.Text.Encoding.UTF8, "application/json")
        });
    }

    private async Task<ToolExecutor> CreateSutWithStockfishStarted()
    {
        await _stockfish.StartAsync();
        return CreateSut();
    }

    private ToolExecutor CreateSut()
    {
        var lichessHttpClient = new HttpClient(new MockHttpHandler((r, c) => _lichessHandler(r, c)))
        {
            BaseAddress = new Uri("https://lichess.org/")
        };
        var openRouterHttpClient = new HttpClient(new MockHttpHandler((r, c) => _openRouterHandler(r, c)));
        var openRouterClient = new OpenRouterClient(openRouterHttpClient, "test-key", "test-model");
        return new ToolExecutor(
            _mockCapture.Object,
            _mockSession.Object,
            openRouterClient,
            lichessHttpClient,
            _stockfish,
            "openai/gpt-4o-mini",
            _mockLogger.Object);
    }

    // ── analyze_position_engine — Full Pipeline ─────────────────────────

    [Fact]
    public async Task AnalyzePositionEngine_EndToEnd_ReturnsFormattedResult()
    {
        var sut = await CreateSutWithStockfishStarted();
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{fen}}"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("bestMove").GetString().Should().Be("e2e4");
        root.GetProperty("depth").GetInt32().Should().Be(20);
        root.TryGetProperty("candidates", out var candidates).Should().BeTrue();
        candidates.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzePositionEngine_InvalidFen_ReturnsFenInvalid()
    {
        var sut = await CreateSutWithStockfishStarted();

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            """{"fen":"not-a-valid-fen"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("fen_invalid");
        root.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePositionEngine_StockfishNotReady_FallsBackToLichess()
    {
        // Stockfish NOT started — should fall back to Lichess
        _lichessHandler = (req, _) =>
        {
            if (req.RequestUri?.PathAndQuery.Contains("cloud-eval") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"depth":28,"pvs":[{"cp":25,"moves":"e2e4 e7e5"}]}""",
                        System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        var sut = CreateSut(); // Stockfish NOT started
        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            """{"fen":"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task AnalyzePositionEngine_MatePosition_ReturnsMateScore()
    {
        var sut = await CreateSutWithStockfishStarted();
        var fen = "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4";

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{fen}}"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("bestMove").GetString().Should().Be("h5f7");
        root.GetProperty("assessment").GetString().Should().Contain("Mate");
    }

    // ── analyze_position_strategic — Full Pipeline ──────────────────────

    [Fact]
    public async Task AnalyzePositionStrategic_ReturnsThemes()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_strategic",
            """{"focus":"general"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("analysis").GetString().Should().NotBeNullOrEmpty();
    }

    // ── Both tools in sequence ──────────────────────────────────────────

    [Fact]
    public async Task BothTools_CalledInSequence_ProducesCompleteAnalysis()
    {
        var sut = await CreateSutWithStockfishStarted();
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // 1. Engine analysis first
        var engineResult = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{fen}}"}""", CancellationToken.None);

        // 2. Then strategic analysis
        var strategicResult = await sut.ExecuteToolAsync("analyze_position_strategic",
            """{"focus":"general","player_color":"white"}""", CancellationToken.None);

        // Both should succeed
        using var engineDoc = JsonDocument.Parse(engineResult);
        engineDoc.RootElement.GetProperty("status").GetString().Should().Be("success");
        engineDoc.RootElement.GetProperty("bestMove").GetString().Should().NotBeNullOrEmpty();

        using var strategicDoc = JsonDocument.Parse(strategicResult);
        strategicDoc.RootElement.GetProperty("status").GetString().Should().Be("success");
        strategicDoc.RootElement.GetProperty("analysis").GetString().Should().NotBeNullOrEmpty();
    }

    // ── Tool availability ───────────────────────────────────────────────

    [Fact]
    public async Task UnknownTool_ReturnsError()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("analyze_position_unknown", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Unknown tool");
    }
}
