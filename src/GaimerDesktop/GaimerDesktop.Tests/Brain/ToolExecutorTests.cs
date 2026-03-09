using System.Net;
using Microsoft.Extensions.Logging;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Brain;

public class ToolExecutorTests
{
    private readonly Mock<IWindowCaptureService> _mockCapture;
    private readonly Mock<ISessionManager> _mockSession;
    private readonly Mock<ILogger<ToolExecutor>> _mockLogger;

    // Mutable handler factories — replaced per test as needed
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _lichessHandler;
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _openRouterHandler;

    public ToolExecutorTests()
    {
        _mockCapture = new Mock<IWindowCaptureService>();
        _mockSession = new Mock<ISessionManager>();
        _mockLogger = new Mock<ILogger<ToolExecutor>>();

        // Default session context
        _mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
            GameType = "chess",
            ConnectorName = "lichess"
        });

        // Default handlers (overridden per test)
        _lichessHandler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
        _openRouterHandler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
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
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            _mockLogger.Object);
    }

    // ── capture_screen ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteToolAsync_CaptureScreen_WhenCapturing_ReturnsBase64()
    {
        _mockCapture.Setup(c => c.IsCapturing).Returns(true);
        _mockCapture.Setup(c => c.CurrentTarget).Returns(new CaptureTarget
        {
            Handle = 1,
            ProcessName = "lichess-app",
            WindowTitle = "Lichess"
        });

        // Fire FrameCaptured event when subscribed
        _mockCapture
            .SetupAdd(c => c.FrameCaptured += It.IsAny<EventHandler<byte[]>>())
            .Callback<EventHandler<byte[]>>(handler =>
            {
                Task.Run(async () =>
                {
                    await Task.Delay(50);
                    handler.Invoke(_mockCapture.Object, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
                });
            });

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("capture_screen", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("captured");
        root.GetProperty("image_base64").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("target").GetString().Should().Be("lichess-app");
    }

    [Fact]
    public async Task ExecuteToolAsync_CaptureScreen_NotCapturing_ReturnsError()
    {
        _mockCapture.Setup(c => c.IsCapturing).Returns(false);

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("capture_screen", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Not currently capturing");
    }

    // ── get_game_state ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteToolAsync_GetGameState_ReturnsSessionContext()
    {
        _mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>
        {
            ToolDefinitions.WebSearch,
            ToolDefinitions.CaptureScreen
        });

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_game_state", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("state").GetString().Should().Be("InGame");
        root.GetProperty("game_id").GetString().Should().Be("test-game-1");
        root.GetProperty("game_type").GetString().Should().Be("chess");
        root.GetProperty("available_tools").GetArrayLength().Should().Be(2);
    }

    // ── get_best_move — Lichess eval responses ──────────────────────────────

    private void SetupLichessResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _lichessHandler = (req, _) =>
        {
            if (req.RequestUri?.PathAndQuery.Contains("cloud-eval") == true)
            {
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };
    }

    [Fact]
    public async Task ExecuteToolAsync_GetBestMove_ValidFen_ReturnsEvaluation()
    {
        SetupLichessResponse("""{"depth":30,"pvs":[{"cp":150,"moves":"e2e4 e7e5"}]}""");

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move",
            """{"fen":"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("evaluation").GetInt32().Should().Be(150);
        root.GetProperty("signal").GetString().Should().Be("opportunity");
    }

    [Fact]
    public async Task ExecuteToolAsync_WebSearch_ValidQuery_ReturnsAnswer()
    {
        _openRouterHandler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"choices":[{"message":{"content":"The Sicilian Defense is a popular response to 1.e4"},"finish_reason":"stop"}]}""",
                System.Text.Encoding.UTF8, "application/json")
        });

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("web_search",
            """{"query":"best chess openings"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("answer").GetString().Should().Contain("Sicilian");
        root.GetProperty("source").GetString().Should().Be("llm_knowledge");
    }

    [Fact]
    public async Task ExecuteToolAsync_PlayerHistory_ReturnsNotAvailable()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("player_history", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_available");
        doc.RootElement.GetProperty("reason").GetString().Should().Contain("Phase 07");
    }

    [Fact]
    public async Task ExecuteToolAsync_PlayerAnalytics_ReturnsNotAvailable()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("player_analytics", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_available");
        doc.RootElement.GetProperty("reason").GetString().Should().Contain("Phase 07");
    }

    [Fact]
    public async Task ExecuteToolAsync_UnknownTool_ReturnsError()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("nonexistent_tool", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Unknown tool");
    }

    // ── ClassifyEvaluation — tested indirectly via Lichess mock cp values ────

    [Fact]
    public async Task GetBestMove_Cp600_ReturnsWinning()
    {
        SetupLichessResponse("""{"depth":20,"pvs":[{"cp":600,"moves":"d2d4"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("signal").GetString().Should().Be("opportunity");
        doc.RootElement.GetProperty("assessment").GetString().Should().Be("Winning");
    }

    [Fact]
    public async Task GetBestMove_Cp300_ReturnsClearAdvantage()
    {
        SetupLichessResponse("""{"depth":20,"pvs":[{"cp":300,"moves":"d2d4"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("assessment").GetString().Should().Be("Clear advantage");
    }

    [Fact]
    public async Task GetBestMove_Cp50_ReturnsEqual()
    {
        SetupLichessResponse("""{"depth":20,"pvs":[{"cp":50,"moves":"d2d4"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("signal").GetString().Should().Be("neutral");
        doc.RootElement.GetProperty("assessment").GetString().Should().Be("Equal");
    }

    [Fact]
    public async Task GetBestMove_CpNeg150_ReturnsSlightDisadvantage()
    {
        SetupLichessResponse("""{"depth":20,"pvs":[{"cp":-150,"moves":"d2d4"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("signal").GetString().Should().Be("blunder");
        doc.RootElement.GetProperty("assessment").GetString().Should().Be("Slight disadvantage");
    }

    [Fact]
    public async Task GetBestMove_CpNeg400_ReturnsClearDisadvantage()
    {
        SetupLichessResponse("""{"depth":20,"pvs":[{"cp":-400,"moves":"d2d4"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("assessment").GetString().Should().Be("Clear disadvantage");
    }

    [Fact]
    public async Task GetBestMove_CpNeg600_ReturnsLosing()
    {
        SetupLichessResponse("""{"depth":20,"pvs":[{"cp":-600,"moves":"d2d4"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("assessment").GetString().Should().Be("Losing");
    }

    [Fact]
    public async Task GetBestMove_MissingFen_ReturnsNotAvailable()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_available");
        doc.RootElement.GetProperty("reason").GetString().Should().ContainEquivalentOf("fen");
    }

    [Fact]
    public async Task GetBestMove_LichessHttpError_ReturnsGracefulFallback()
    {
        SetupLichessResponse("{}", HttpStatusCode.InternalServerError);

        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_available");
    }

    [Fact]
    public async Task GetBestMove_PositiveMate_ReturnsOpportunity()
    {
        SetupLichessResponse("""{"depth":30,"pvs":[{"mate":3,"moves":"d2d4 e7e5 d4e5"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("signal").GetString().Should().Be("opportunity");
        doc.RootElement.GetProperty("assessment").GetString().Should().Contain("Mate in 3");
        doc.RootElement.GetProperty("evaluation").GetInt32().Should().Be(10000);
    }

    [Fact]
    public async Task GetBestMove_NegativeMate_ReturnsBlunder()
    {
        SetupLichessResponse("""{"depth":30,"pvs":[{"mate":-2,"moves":"e7e5"}]}""");
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move", """{"fen":"test"}""", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("signal").GetString().Should().Be("blunder");
        doc.RootElement.GetProperty("assessment").GetString().Should().Contain("Getting mated in 2");
        doc.RootElement.GetProperty("evaluation").GetInt32().Should().Be(-10000);
    }

    [Fact]
    public async Task WebSearch_MissingQuery_ReturnsError()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("web_search", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("reason").GetString().Should().ContainEquivalentOf("query");
    }

    // ── GetAvailableToolDefinitions ─────────────────────────────────────────

    [Theory]
    [InlineData(SessionState.OutGame, 3)]
    [InlineData(SessionState.InGame, 7)]
    public void GetAvailableToolDefinitions_ReturnsCorrectCount(SessionState state, int expected)
    {
        var allTools = new List<ToolDefinition>
        {
            ToolDefinitions.WebSearch,
            ToolDefinitions.PlayerHistory,
            ToolDefinitions.PlayerAnalytics,
            ToolDefinitions.CaptureScreen,
            ToolDefinitions.GetGameState,
            ToolDefinitions.AnalyzePositionEngine,
            ToolDefinitions.AnalyzePositionStrategic,
        };

        var filtered = state == SessionState.OutGame
            ? allTools.Where(t => !t.RequiresInGame).ToList()
            : allTools;

        _mockSession.Setup(s => s.GetAvailableTools()).Returns(filtered);

        var result = ToolExecutor.GetAvailableToolDefinitions(_mockSession.Object);
        result.Should().HaveCount(expected);
    }
}
