using System.Net;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Tests.Helpers;
using WitnessDesktop.Tests.ViewModels;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Tests.Brain;

/// <summary>
/// Phase 14 gap-closing tests for pipeline wiring, tool integration,
/// FEN extraction, prompt builder usage, telemetry, and chat reply routing.
/// </summary>
public class Phase14GapClosingTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Gap 1: game_journal tool — additional edge cases
    // ═══════════════════════════════════════════════════════════════════════

    #region Gap 1: game_journal ToolExecutor

    private static ToolExecutor CreateToolExecutor(
        IGameJournalService? journal = null,
        ITelemetryService? telemetry = null)
    {
        var mockCapture = new Mock<IWindowCaptureService>();
        var mockSession = new Mock<ISessionManager>();
        mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameId = "test-game-1",
            GameType = "chess",
            ConnectorName = "lichess"
        });
        mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());

        var httpClient = new HttpClient(MockHttpHandler.FromJson("{}"));
        var openRouterClient = new OpenRouterClient(httpClient, "test-key", "test-model");
        var mockLogger = new Mock<ILogger<ToolExecutor>>();

        return new ToolExecutor(
            mockCapture.Object,
            mockSession.Object,
            openRouterClient,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object,
            telemetry: telemetry,
            gameJournal: journal);
    }

    [Fact]
    public async Task GameJournal_MultipleEntries_ReturnsAllInOrder()
    {
        var journal = new GameJournalService();
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
            MoveNotation: "e4",
            Description: "King's pawn opening",
            Evaluation: "+0.30",
            Timestamp: DateTimeOffset.UtcNow.AddMinutes(-2)));
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 2,
            Fen: "rnbqkbnr/pppppppp/8/8/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 1",
            MoveNotation: "Nf3",
            Description: "Knight development",
            Evaluation: "+0.25",
            Timestamp: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var sut = CreateToolExecutor(journal: journal);
        var result = await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("entry_count").GetInt32().Should().Be(2);
        root.GetProperty("entries").GetArrayLength().Should().Be(2);

        var first = root.GetProperty("entries")[0];
        first.GetProperty("move").GetInt32().Should().Be(1);
        first.GetProperty("notation").GetString().Should().Be("e4");
        first.GetProperty("eval").GetString().Should().Be("+0.30");

        var second = root.GetProperty("entries")[1];
        second.GetProperty("move").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GameJournal_SummaryReflectsEntryCount()
    {
        var journal = new GameJournalService();
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
            MoveNotation: null,
            Description: "Opening move",
            Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        var sut = CreateToolExecutor(journal: journal);
        var result = await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var summary = doc.RootElement.GetProperty("summary").GetString();
        summary.Should().Contain("1 positions analyzed");
    }

    [Fact]
    public async Task GameJournal_EntryWithNullFen_SerializesAsNull()
    {
        var journal = new GameJournalService();
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: null,
            MoveNotation: null,
            Description: "Position unclear",
            Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        var sut = CreateToolExecutor(journal: journal);
        var result = await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var entry = doc.RootElement.GetProperty("entries")[0];
        entry.GetProperty("fen").ValueKind.Should().Be(JsonValueKind.Null);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // Gap 2: BrainEventRouter FEN extraction from analysis text
    // ═══════════════════════════════════════════════════════════════════════

    #region Gap 2: FEN extraction

    [Fact]
    public void ExtractFenFromAnalysis_ValidFenInText_ExtractsFen()
    {
        var text = "The current position is rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1 after e4.";
        var fen = BrainEventRouter.ExtractFenFromAnalysis(text);

        fen.Should().Be("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");
    }

    [Fact]
    public void ExtractFenFromAnalysis_NoFenInText_ReturnsNull()
    {
        var text = "The board shows a standard opening with pawns advanced.";
        var fen = BrainEventRouter.ExtractFenFromAnalysis(text);

        fen.Should().BeNull();
    }

    [Fact]
    public void ExtractFenFromAnalysis_MultipleFenLikeStrings_ExtractsFirstMatch()
    {
        var text = "Position: rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1 was reached. " +
                   "After Nf3: rnbqkbnr/pppppppp/8/8/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 1 is the new position.";
        var fen = BrainEventRouter.ExtractFenFromAnalysis(text);

        fen.Should().Be("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");
    }

    [Fact]
    public void ExtractFenFromAnalysis_FenWithEnPassantSquare_Extracts()
    {
        var text = "[FEN: rnbqkbnr/pppp1ppp/8/4pP2/4P3/8/PPPP2PP/RNBQKBNR w KQkq e6 0 3]";
        var fen = BrainEventRouter.ExtractFenFromAnalysis(text);

        fen.Should().Be("rnbqkbnr/pppp1ppp/8/4pP2/4P3/8/PPPP2PP/RNBQKBNR w KQkq e6 0 3");
    }

    [Fact]
    public void ExtractFenFromAnalysis_FenWithNoCastling_Extracts()
    {
        var text = "Position: r1bqkb1r/pppppppp/2n2n2/8/4P3/5N2/PPPP1PPP/RNBQKB1R w - - 4 3";
        var fen = BrainEventRouter.ExtractFenFromAnalysis(text);

        // Castling field is "-" which matches [KQkq-]+
        fen.Should().NotBeNull();
        fen.Should().Contain("w - -");
    }

    [Fact]
    public void ExtractFenFromAnalysis_EmptyString_ReturnsNull()
    {
        var fen = BrainEventRouter.ExtractFenFromAnalysis("");
        fen.Should().BeNull();
    }

    [Fact]
    public void IsStartingFen_StandardOpening_ReturnsTrue()
    {
        var result = BrainEventRouter.IsStartingFenForTest(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsStartingFen_NonStartingPosition_ReturnsFalse()
    {
        var result = BrainEventRouter.IsStartingFenForTest(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");
        result.Should().BeFalse();
    }

    [Fact]
    public void RouteBrainResult_ImageAnalysis_WithFen_JournalEntryHasFen()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var mockJournal = new Mock<IGameJournalService>();
        mockJournal.Setup(j => j.EntryCount).Returns(0);
        mockJournal.Setup(j => j.ValidateTemporalConsistency(It.IsAny<string?>()))
            .Returns(new TemporalValidation(true, null, "Consistent"));

        var router = new BrainEventRouter(
            mockTimeline.Object,
            gameJournal: mockJournal.Object);

        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = $"Analysis of position {fen} shows White played e4.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.Fen == fen)), Times.Once);
    }

    [Fact]
    public void RouteBrainResult_ImageAnalysis_WithoutFen_JournalEntryHasNullFen()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var mockJournal = new Mock<IGameJournalService>();
        mockJournal.Setup(j => j.EntryCount).Returns(0);
        mockJournal.Setup(j => j.ValidateTemporalConsistency(It.IsAny<string?>()))
            .Returns(new TemporalValidation(true, null, "Consistent"));

        var router = new BrainEventRouter(
            mockTimeline.Object,
            gameJournal: mockJournal.Object);

        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "The board shows a balanced position with no clear advantage.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.Fen == null)), Times.Once);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // Gap 3: OpenRouterBrainService uses BrainPromptBuilder
    // ═══════════════════════════════════════════════════════════════════════

    #region Gap 3: BrainPromptBuilder usage

    private static OpenRouterBrainService CreateBrainServiceWithMocks(
        MockHttpHandler httpHandler,
        Mock<ISessionManager>? sessionMock = null,
        Mock<IBrainPromptBuilder>? promptBuilderMock = null,
        ITelemetryService? telemetry = null,
        IGameJournalService? journal = null,
        IBrainContextService? brainContext = null)
    {
        sessionMock ??= CreateDefaultSessionMock();
        var httpClient = new HttpClient(httpHandler);
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var toolExecutor = new ToolExecutor(
            Mock.Of<IWindowCaptureService>(),
            sessionMock.Object,
            client,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object,
            telemetry: telemetry,
            gameJournal: journal);

        return new OpenRouterBrainService(
            client, toolExecutor, sessionMock.Object,
            brainPromptBuilder: promptBuilderMock?.Object,
            telemetry: telemetry,
            gameJournal: journal,
            brainContext: brainContext,
            maxOpenRouterRetries: 0,
            openRouterRetryBaseDelay: TimeSpan.Zero);
    }

    private static Mock<ISessionManager> CreateDefaultSessionMock()
    {
        var mock = new Mock<ISessionManager>();
        mock.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameType = "chess",
            AgentKey = "chess"
        });
        mock.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());
        return mock;
    }

    private static async Task<BrainResult> ReadResultWithTimeout(
        OpenRouterBrainService sut, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        return await sut.Results.ReadAsync(cts.Token);
    }

    [Fact]
    public async Task SubmitImageAsync_CallsBuildSystemPrompt_WithAgent()
    {
        var promptMock = new Mock<IBrainPromptBuilder>();
        promptMock.Setup(p => p.BuildSystemPrompt(
            It.IsAny<Agent>(),
            It.IsAny<IReadOnlyList<BrainEvent>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>()))
            .Returns("System prompt text");
        promptMock.Setup(p => p.BuildUserPrompt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns("User prompt text");

        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"analysis"},"finish_reason":"stop"}]}""");

        using var sut = CreateBrainServiceWithMocks(handler, promptBuilderMock: promptMock);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        promptMock.Verify(p => p.BuildSystemPrompt(
            It.Is<Agent>(a => a.Key == "chess"),
            It.IsAny<IReadOnlyList<BrainEvent>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitImageAsync_CallsBuildUserPrompt_WithGameTypeAndMoveNumber()
    {
        var promptMock = new Mock<IBrainPromptBuilder>();
        promptMock.Setup(p => p.BuildSystemPrompt(
            It.IsAny<Agent>(),
            It.IsAny<IReadOnlyList<BrainEvent>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>()))
            .Returns("System prompt");
        promptMock.Setup(p => p.BuildUserPrompt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns("User prompt");

        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"analysis"},"finish_reason":"stop"}]}""");

        using var sut = CreateBrainServiceWithMocks(handler, promptBuilderMock: promptMock);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        promptMock.Verify(p => p.BuildUserPrompt("chess", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task SubmitImageAsync_NoAgent_UsesFallbackPersonality()
    {
        // Set AgentKey to empty — no agent found
        var sessionMock = new Mock<ISessionManager>();
        sessionMock.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameType = "chess",
            AgentKey = ""
        });
        sessionMock.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());

        var promptMock = new Mock<IBrainPromptBuilder>();

        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"analysis"},"finish_reason":"stop"}]}""");

        using var sut = CreateBrainServiceWithMocks(handler, sessionMock: sessionMock, promptBuilderMock: promptMock);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        // BuildSystemPrompt should NOT be called when no agent is found — uses GetBrainPersonality() fallback
        promptMock.Verify(p => p.BuildSystemPrompt(
            It.IsAny<Agent>(),
            It.IsAny<IReadOnlyList<BrainEvent>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitImageAsync_PassesJournalSummaryToPromptBuilder()
    {
        var journal = new GameJournalService();
        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1, Fen: null, MoveNotation: null,
            Description: "Opening", Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        var promptMock = new Mock<IBrainPromptBuilder>();
        promptMock.Setup(p => p.BuildSystemPrompt(
            It.IsAny<Agent>(),
            It.IsAny<IReadOnlyList<BrainEvent>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>()))
            .Returns("System prompt");
        promptMock.Setup(p => p.BuildUserPrompt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns("User prompt");

        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"analysis"},"finish_reason":"stop"}]}""");

        using var sut = CreateBrainServiceWithMocks(handler, promptBuilderMock: promptMock, journal: journal);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        promptMock.Verify(p => p.BuildSystemPrompt(
            It.IsAny<Agent>(),
            It.IsAny<IReadOnlyList<BrainEvent>>(),
            It.Is<string?>(s => s != null && s.Contains("1 positions analyzed")),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>()),
            Times.Once);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // Gap 4: Brain reply via Channel triggers ChatMessages in MainViewModel
    // ═══════════════════════════════════════════════════════════════════════

    #region Gap 4: BrainChatReplyReceived -> ChatMessages

    [Fact]
    public void BrainChatReplyReceived_AddsAssistantMessageToChatMessages()
    {
        // Use a real BrainEventRouter with mock timeline to fire the event
        var mockTimeline = new Mock<ITimelineFeed>();
        var router = new BrainEventRouter(mockTimeline.Object);

        // Track what the event sends
        string? receivedReply = null;
        router.BrainChatReplyReceived += reply => receivedReply = reply;

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "The best move is Nf3 — developing the knight toward the center.",
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        receivedReply.Should().NotBeNull();
        receivedReply.Should().Be("The best move is Nf3 — developing the knight toward the center.");
    }

    [Fact]
    public void BrainChatReplyReceived_DoesNotFireOnError()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var router = new BrainEventRouter(mockTimeline.Object);

        string? receivedReply = null;
        router.BrainChatReplyReceived += reply => receivedReply = reply;

        var result = new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = "API error occurred",
            Priority = BrainResultPriority.Silent,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        receivedReply.Should().BeNull("BrainChatReplyReceived should not fire on Error type");
    }

    [Fact]
    public void BrainChatReplyReceived_DoesNotFireOnProactiveAlert()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var router = new BrainEventRouter(mockTimeline.Object);

        string? receivedReply = null;
        router.BrainChatReplyReceived += reply => receivedReply = reply;

        var result = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            Hint = new BrainHint
            {
                Signal = "danger",
                Urgency = "high",
                Summary = "Blunder detected",
                Evaluation = -300
            },
            AnalysisText = "Blunder detected",
            Priority = BrainResultPriority.Interrupt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        receivedReply.Should().BeNull("BrainChatReplyReceived should not fire on ProactiveAlert");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // Gap 5: Telemetry wiring verification
    // ═══════════════════════════════════════════════════════════════════════

    #region Gap 5: Telemetry wiring

    [Fact]
    public async Task ToolExecutor_TracksTelemetry_OnToolCalled()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var sut = CreateToolExecutor(telemetry: mockTelemetry.Object);

        await sut.ExecuteToolAsync("get_game_state", "{}", CancellationToken.None);

        mockTelemetry.Verify(t => t.TrackEvent(
            "tool", "called",
            It.Is<Dictionary<string, string>>(d => d["toolName"] == "get_game_state")),
            Times.Once);
    }

    [Fact]
    public async Task ToolExecutor_TracksTelemetry_OnToolCompleted()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var sut = CreateToolExecutor(telemetry: mockTelemetry.Object);

        await sut.ExecuteToolAsync("get_game_state", "{}", CancellationToken.None);

        mockTelemetry.Verify(t => t.TrackEvent(
            "tool", "completed",
            It.Is<Dictionary<string, string>>(d =>
                d["toolName"] == "get_game_state" &&
                d.ContainsKey("duration_ms"))),
            Times.Once);
    }

    [Fact]
    public async Task ToolExecutor_TracksTelemetry_OnGameJournalTool()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var journal = new GameJournalService();
        var sut = CreateToolExecutor(journal: journal, telemetry: mockTelemetry.Object);

        await sut.ExecuteToolAsync("game_journal", "{}", CancellationToken.None);

        mockTelemetry.Verify(t => t.TrackEvent(
            "tool", "called",
            It.Is<Dictionary<string, string>>(d => d["toolName"] == "game_journal")),
            Times.Once);
        mockTelemetry.Verify(t => t.TrackEvent(
            "tool", "completed",
            It.Is<Dictionary<string, string>>(d => d["toolName"] == "game_journal")),
            Times.Once);
    }

    [Fact]
    public void BrainEventRouter_TracksTelemetry_OnRouteBrainResult()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var mockTelemetry = new Mock<ITelemetryService>();

        var router = new BrainEventRouter(
            mockTimeline.Object,
            telemetry: mockTelemetry.Object);

        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed",
            CorrelationId = "abc12345",
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        mockTelemetry.Verify(t => t.TrackEvent(
            "router", "result_routed",
            It.Is<Dictionary<string, string>>(d =>
                d["type"] == "ImageAnalysis" &&
                d["correlationId"] == "abc12345")),
            Times.Once);
    }

    [Fact]
    public void BrainEventRouter_TracksTelemetry_OnToolResult()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var mockTelemetry = new Mock<ITelemetryService>();

        var router = new BrainEventRouter(
            mockTimeline.Object,
            telemetry: mockTelemetry.Object);

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "Engine says Nf3",
            CorrelationId = "def67890",
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        mockTelemetry.Verify(t => t.TrackEvent(
            "router", "result_routed",
            It.Is<Dictionary<string, string>>(d =>
                d["type"] == "ToolResult" &&
                d["correlationId"] == "def67890")),
            Times.Once);
    }

    [Fact]
    public void BrainEventRouter_TracksTelemetry_WithNoneCorrelationId_WhenNull()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var mockTelemetry = new Mock<ITelemetryService>();

        var router = new BrainEventRouter(
            mockTimeline.Object,
            telemetry: mockTelemetry.Object);

        var result = new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = "Error occurred",
            CorrelationId = null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        mockTelemetry.Verify(t => t.TrackEvent(
            "router", "result_routed",
            It.Is<Dictionary<string, string>>(d =>
                d["correlationId"] == "none")),
            Times.Once);
    }

    [Fact]
    public async Task OpenRouterBrainService_TracksTelemetry_OnSubmitImage()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"analysis"},"finish_reason":"stop"}]}""");

        using var sut = CreateBrainServiceWithMocks(handler, telemetry: mockTelemetry.Object);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        mockTelemetry.Verify(t => t.TrackEvent(
            "brain", "submit_image",
            It.Is<Dictionary<string, string>>(d =>
                d.ContainsKey("correlationId") &&
                d.ContainsKey("bytes"))),
            Times.Once);
    }

    [Fact]
    public async Task OpenRouterBrainService_TracksTelemetry_OnResponseReceived()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"analysis"},"finish_reason":"stop"}]}""");

        using var sut = CreateBrainServiceWithMocks(handler, telemetry: mockTelemetry.Object);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        mockTelemetry.Verify(t => t.TrackEvent(
            "brain", "response_received",
            It.Is<Dictionary<string, string>>(d =>
                d.ContainsKey("correlationId") &&
                d["toolTurns"] == "0")),
            Times.Once);
    }

    [Fact]
    public async Task OpenRouterBrainService_TracksTelemetry_OnError()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var handler = MockHttpHandler.FromJson(
            """{"error":"server error"}""", HttpStatusCode.InternalServerError);

        using var sut = CreateBrainServiceWithMocks(handler, telemetry: mockTelemetry.Object);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "context");
        await ReadResultWithTimeout(sut);

        mockTelemetry.Verify(t => t.TrackEvent(
            "brain", "response_error",
            It.Is<Dictionary<string, string>>(d =>
                d.ContainsKey("correlationId") &&
                d.ContainsKey("error"))),
            Times.Once);
    }

    [Fact]
    public void GameJournalService_TracksTelemetry_OnAddEntry()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var journal = new GameJournalService(mockTelemetry.Object);

        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
            MoveNotation: null,
            Description: "e4 opening",
            Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        mockTelemetry.Verify(t => t.TrackEvent(
            "journal", "entry_added",
            It.Is<Dictionary<string, string>>(d =>
                d["moveNumber"] == "1" &&
                d["hasFen"] == "True")),
            Times.Once);
    }

    [Fact]
    public void GameJournalService_TracksTelemetry_WithHasFenFalse_WhenNoFen()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var journal = new GameJournalService(mockTelemetry.Object);

        journal.AddEntry(new GameJournalEntry(
            MoveNumber: 1,
            Fen: null,
            MoveNotation: null,
            Description: "Position unclear",
            Evaluation: null,
            Timestamp: DateTimeOffset.UtcNow));

        mockTelemetry.Verify(t => t.TrackEvent(
            "journal", "entry_added",
            It.Is<Dictionary<string, string>>(d =>
                d["hasFen"] == "False")),
            Times.Once);
    }

    [Fact]
    public void BrainEventRouter_NewGameDetection_TracksTelemetry()
    {
        var mockTimeline = new Mock<ITimelineFeed>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockJournal = new Mock<IGameJournalService>();
        var mockDiffService = new Mock<IFrameDiffService>();

        // Setup: journal has history, last position was non-starting
        mockJournal.Setup(j => j.EntryCount).Returns(5);
        mockJournal.Setup(j => j.GetLatestFen()).Returns(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"); // non-starting
        mockJournal.Setup(j => j.GetSummary()).Returns("5 moves played");
        mockJournal.Setup(j => j.ValidateTemporalConsistency(It.IsAny<string?>()))
            .Returns(new TemporalValidation(true, null, "Consistent"));

        var router = new BrainEventRouter(
            mockTimeline.Object,
            telemetry: mockTelemetry.Object,
            gameJournal: mockJournal.Object,
            frameDiffService: mockDiffService.Object);

        // Submit starting position FEN — should trigger new game detection
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1 — starting position.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        mockTelemetry.Verify(t => t.TrackEvent(
            "game", "new_game_detected",
            It.IsAny<Dictionary<string, string>>()),
            Times.Once);

        mockJournal.Verify(j => j.Clear(), Times.Once);
        mockDiffService.Verify(d => d.ResetHash(), Times.Once);
    }

    #endregion
}
