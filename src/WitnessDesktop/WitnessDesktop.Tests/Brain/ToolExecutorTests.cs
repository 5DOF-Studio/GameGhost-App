using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Brain;

public class ToolExecutorTests
{
    private readonly Mock<IWindowCaptureService> _mockCapture;
    private readonly Mock<ISessionManager> _mockSession;
    private readonly Mock<ILogger<ToolExecutor>> _mockLogger;

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

        _openRouterHandler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
    }

    private ToolExecutor CreateSut(IGameJournalService? journal = null)
    {
        var openRouterHttpClient = new HttpClient(new MockHttpHandler((r, c) => _openRouterHandler(r, c)));
        var openRouterClient = new OpenRouterClient(openRouterHttpClient, "test-key", "test-model");
        return new ToolExecutor(
            _mockCapture.Object,
            _mockSession.Object,
            openRouterClient,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            _mockLogger.Object,
            gameJournal: journal);
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

    // ── get_best_move — now returns unknown tool error ────────────────────

    [Fact]
    public async Task ExecuteToolAsync_GetBestMove_ReturnsUnknownToolError()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("get_best_move",
            """{"fen":"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"}""",
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("error").GetString().Should().Contain("Unknown tool");
        root.GetProperty("tool_name").GetString().Should().Be("get_best_move");
    }

    // ── web_search ──────────────────────────────────────────────────────────

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
    public async Task ExecuteToolAsync_PlayerHistory_Removed_ReturnsUnknown()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("player_history", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("Unknown tool");
    }

    [Fact]
    public async Task ExecuteToolAsync_PlayerAnalytics_Removed_ReturnsUnknown()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("player_analytics", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("Unknown tool");
    }

    [Fact]
    public async Task ExecuteToolAsync_UnknownTool_ReturnsError()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteToolAsync("nonexistent_tool", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Unknown tool");
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

    // ── game_journal tool ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteToolAsync_GameJournal_ReturnsEntries()
    {
        var journal = new GameJournalService();
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
            MoveNotation: null,
            Description: "e4 opening",
            Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        var sut = CreateSut(journal: journal);
        var result = await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("entry_count").GetInt32().Should().Be(1);
        root.GetProperty("entries").GetArrayLength().Should().Be(1);
        root.GetProperty("summary").GetString().Should().Contain("1 positions analyzed");

        var entry = root.GetProperty("entries")[0];
        entry.GetProperty("move").GetInt32().Should().Be(1);
        entry.GetProperty("description").GetString().Should().Be("e4 opening");
    }

    [Fact]
    public async Task ExecuteToolAsync_GameJournal_EmptyJournal_ReturnsEmptyEntries()
    {
        var journal = new GameJournalService();
        var sut = CreateSut(journal: journal);
        var result = await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("entry_count").GetInt32().Should().Be(0);
        root.GetProperty("entries").GetArrayLength().Should().Be(0);
        root.GetProperty("summary").GetString().Should().Be("No positions recorded yet.");
    }

    [Fact]
    public async Task ExecuteToolAsync_GameJournal_NoJournalService_ReturnsNotAvailable()
    {
        var sut = CreateSut(journal: null);
        var result = await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("not_available");
    }

    // ── game_journal generalized (RASA) ────────────────────────────────────

    [Fact]
    public async Task GameJournal_AddAction_ReturnsLogged()
    {
        var sut = CreateSut();
        var args = """{"action":"add","entry_type":"event","content":"Player entered the cave","tags":["exploration","cave"]}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("logged");
        root.GetProperty("entry_type").GetString().Should().Be("event");
    }

    [Fact]
    public async Task GameJournal_AddAction_MissingContent_ReturnsError()
    {
        var sut = CreateSut();
        var args = """{"action":"add","entry_type":"event"}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("reason").GetString().Should().Contain("Missing");
    }

    [Fact]
    public async Task GameJournal_AddAction_DefaultsEntryTypeToObservation()
    {
        var sut = CreateSut();
        var args = """{"action":"add","content":"Enemy spotted near bridge"}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("logged");
        root.GetProperty("entry_type").GetString().Should().Be("observation");
    }

    [Fact]
    public async Task GameJournal_QueryAction_ReturnsNotAvailable()
    {
        var sut = CreateSut();
        var args = """{"action":"query","content":"what happened in the cave"}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("not_available");
        root.GetProperty("reason").GetString().Should().Contain("Knowledge base");
    }

    [Fact]
    public async Task GameJournal_NoAction_FallsBackToChessMode()
    {
        // No action specified — falls back to chess read-only mode
        var sut = CreateSut(journal: null);
        var args = "{}";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        // With no journal service, chess fallback returns not_available
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_available");
    }

    [Fact]
    public async Task GameJournal_NoAction_WithChessJournal_ReturnsChessEntries()
    {
        var journal = new GameJournalService();
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
            MoveNotation: "e4",
            Description: "King's pawn opening",
            Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        var sut = CreateSut(journal: journal);
        var args = "{}";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("entry_count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GameJournal_SummaryAction_NoChessJournal_ReturnsNotAvailable()
    {
        var sut = CreateSut(journal: null);
        var args = """{"action":"summary"}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("not_available");
    }

    [Fact]
    public async Task GameJournal_SummaryAction_WithChessJournal_ReturnsChessData()
    {
        var journal = new GameJournalService();
        var sut = CreateSut(journal: journal);
        var args = """{"action":"summary"}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task GameJournal_UnknownAction_ReturnsError()
    {
        var sut = CreateSut();
        var args = """{"action":"delete"}""";

        var result = await sut.ExecuteToolAsync("game_journal", args, CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("reason").GetString().Should().Contain("Unknown journal action");
    }

    // ── GetAvailableToolDefinitions ─────────────────────────────────────────

    [Theory]
    [InlineData(SessionState.OutGame, 1)]
    [InlineData(SessionState.InGame, 6)]
    public void GetAvailableToolDefinitions_ReturnsCorrectCount(SessionState state, int expected)
    {
        var allTools = new List<ToolDefinition>
        {
            ToolDefinitions.WebSearch,
            ToolDefinitions.CaptureScreen,
            ToolDefinitions.GetGameState,
            ToolDefinitions.AnalyzePositionEngine,
            ToolDefinitions.AnalyzePositionStrategic,
            ToolDefinitions.GameJournal,
        };

        var filtered = state == SessionState.OutGame
            ? allTools.Where(t => !t.RequiresInGame).ToList()
            : allTools;

        _mockSession.Setup(s => s.GetAvailableTools()).Returns(filtered);

        var result = ToolExecutor.GetAvailableToolDefinitions(_mockSession.Object);
        result.Should().HaveCount(expected);
    }
}
