using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Chess;

public class ToolExecutorChessTests
{
    private const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string AfterE4Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
    private const string InvalidFen = "not a valid fen string";

    private readonly Mock<IWindowCaptureService> _mockCapture;
    private readonly Mock<ISessionManager> _mockSession;
    private readonly Mock<ILogger<ToolExecutor>> _mockLogger;
    private readonly MockStockfishService _mockStockfish;

    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _lichessHandler;
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _openRouterHandler;

    public ToolExecutorChessTests()
    {
        _mockCapture = new Mock<IWindowCaptureService>();
        _mockSession = new Mock<ISessionManager>();
        _mockLogger = new Mock<ILogger<ToolExecutor>>();
        _mockStockfish = new MockStockfishService();

        _mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
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
                """{"choices":[{"message":{"content":"Strategic analysis: This is a balanced position."},"finish_reason":"stop"}]}""",
                System.Text.Encoding.UTF8, "application/json")
        });
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
            _mockStockfish,
            "openai/gpt-4o-mini",
            _mockLogger.Object);
    }

    // ── analyze_position_engine ──────────────────────────────────────────────

    [Fact]
    public async Task AnalyzePositionEngine_ValidFen_ReturnsBestMoveAndCandidates()
    {
        await _mockStockfish.StartAsync();
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{StartingFen}}"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("bestMove").GetString().Should().Be("e2e4");
        root.TryGetProperty("evaluation", out var eval).Should().BeTrue();
        root.TryGetProperty("candidates", out var candidates).Should().BeTrue();
        candidates.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzePositionEngine_InvalidFen_ReturnsFenInvalidStatus()
    {
        await _mockStockfish.StartAsync();
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            """{"fen":"not a valid fen"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("fen_invalid");
        root.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
        root.TryGetProperty("hint", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePositionEngine_StockfishNotReady_FallsBackToLichess()
    {
        // Don't call StartAsync — Stockfish is not ready
        _lichessHandler = (req, _) =>
        {
            if (req.RequestUri?.PathAndQuery.Contains("cloud-eval") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"depth":30,"pvs":[{"cp":150,"moves":"e2e4 e7e5"}]}""",
                        System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{AfterE4Fen}}"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        // Should fall back to Lichess and return success
        root.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task AnalyzePositionEngine_MatePosition_ReturnsMateScore()
    {
        await _mockStockfish.StartAsync();
        var sut = CreateSut();

        // Scholar's mate setup — MockStockfishService returns mate-in-1
        var mateFen = "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4";
        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{mateFen}}"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("bestMove").GetString().Should().Be("h5f7");

        var eval = root.GetProperty("evaluation");
        eval.GetProperty("type").GetString().Should().Be("mate");
    }

    [Fact]
    public async Task AnalyzePositionEngine_WithDepthParam_PassesToEngine()
    {
        await _mockStockfish.StartAsync();
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{StartingFen}}","depth":15}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task AnalyzePositionEngine_WithNumLines_ReturnsCandidates()
    {
        await _mockStockfish.StartAsync();
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            $$"""{"fen":"{{StartingFen}}","num_lines":3}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.TryGetProperty("candidates", out var candidates).Should().BeTrue();
        // MockStockfishService may return 1 variation, but the tool should still work
        candidates.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task AnalyzePositionEngine_MissingFen_ReturnsError()
    {
        await _mockStockfish.StartAsync();
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_engine",
            """{}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("fen_invalid");
    }

    // ── analyze_position_strategic ───────────────────────────────────────────

    [Fact]
    public async Task AnalyzePositionStrategic_ReturnsAnalysis()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_strategic",
            """{"focus":"general","player_color":"white"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.TryGetProperty("analysis", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePositionStrategic_NoParams_StillWorks()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteToolAsync("analyze_position_strategic",
            """{}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    // ── Legacy get_best_move backward compat ─────────────────────────────────

    [Fact]
    public async Task LegacyGetBestMove_StillWorks()
    {
        _lichessHandler = (req, _) =>
        {
            if (req.RequestUri?.PathAndQuery.Contains("cloud-eval") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"depth":30,"pvs":[{"cp":50,"moves":"e2e4 e7e5"}]}""",
                        System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move",
            """{"fen":"test"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("evaluation").GetInt32().Should().Be(50);
    }
}
