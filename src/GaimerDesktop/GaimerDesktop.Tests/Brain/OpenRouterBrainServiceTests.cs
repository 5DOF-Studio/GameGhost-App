using System.Net;
using Microsoft.Extensions.Logging;
using GaimerDesktop.Models;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Brain;
using GaimerDesktop.Services.Chess;
using GaimerDesktop.Tests.Helpers;

namespace GaimerDesktop.Tests.Brain;

public class OpenRouterBrainServiceTests : IDisposable
{
    private readonly Mock<ISessionManager> _mockSession;
    private readonly OpenRouterBrainService _sut;

    public OpenRouterBrainServiceTests()
    {
        _mockSession = new Mock<ISessionManager>();
        _mockSession.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameType = "chess"
        });
        _mockSession.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());

        var httpHandler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"test"},"finish_reason":"stop"}]}""");
        var httpClient = new HttpClient(httpHandler);
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var lichessHandler = MockHttpHandler.FromJson("{}");
        var lichessClient = new HttpClient(lichessHandler) { BaseAddress = new Uri("https://lichess.org/") };
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var toolExecutor = new ToolExecutor(
            Mock.Of<IWindowCaptureService>(),
            _mockSession.Object,
            client,
            lichessClient,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object);

        _sut = new OpenRouterBrainService(client, toolExecutor, _mockSession.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Factory Helpers ──────────────────────────────────────────────────────

    private static Mock<ISessionManager> CreateDefaultSessionMock()
    {
        var mock = new Mock<ISessionManager>();
        mock.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameType = "chess"
        });
        mock.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>());
        return mock;
    }

    private static OpenRouterBrainService CreateServiceWithHandler(
        MockHttpHandler handler, Mock<ISessionManager>? sessionMock = null)
    {
        sessionMock ??= CreateDefaultSessionMock();
        var httpClient = new HttpClient(handler);
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var lichessHandler = MockHttpHandler.FromJson("{}");
        var lichessClient = new HttpClient(lichessHandler) { BaseAddress = new Uri("https://lichess.org/") };
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var toolExecutor = new ToolExecutor(
            Mock.Of<IWindowCaptureService>(),
            sessionMock.Object,
            client,
            lichessClient,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object);

        return new OpenRouterBrainService(client, toolExecutor, sessionMock.Object);
    }

    private static async Task<BrainResult> ReadResultWithTimeout(
        OpenRouterBrainService sut, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        return await sut.Results.ReadAsync(cts.Token);
    }

    // ── TruncateForVoice (private static, tested via reflection) ────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void TruncateForVoice_NullOrEmpty_ReturnsEmpty(string? input, string expected)
    {
        var result = ReflectionHelper.InvokePrivateStatic<string>(
            typeof(OpenRouterBrainService), "TruncateForVoice", input!, 200);

        result.Should().Be(expected);
    }

    [Fact]
    public void TruncateForVoice_ShortText_ReturnsAsIs()
    {
        var result = ReflectionHelper.InvokePrivateStatic<string>(
            typeof(OpenRouterBrainService), "TruncateForVoice", "Short.", 200);

        result.Should().Be("Short.");
    }

    [Fact]
    public void TruncateForVoice_SentenceBoundary_TruncatesAtPeriod()
    {
        // Build text: "First sentence. " (17 chars) + padding to exceed maxLen=50
        var text = "First sentence. This is the second sentence that goes well beyond the limit and keeps going.";
        var result = ReflectionHelper.InvokePrivateStatic<string>(
            typeof(OpenRouterBrainService), "TruncateForVoice", text, 50);

        // Should truncate at the first ". " boundary past 50% of maxLen (25 chars)
        result.Should().EndWith(".");
        result!.Length.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void TruncateForVoice_NoGoodBoundary_HardTruncatesWithEllipsis()
    {
        // Long text with no punctuation
        var text = new string('a', 300);
        var result = ReflectionHelper.InvokePrivateStatic<string>(
            typeof(OpenRouterBrainService), "TruncateForVoice", text, 200);

        result.Should().EndWith("...");
        result!.Length.Should().Be(200);
    }

    // ── CancelAll / IsBusy ──────────────────────────────────────────────────

    [Fact]
    public void CancelAll_CancelsOldCts()
    {
        // Simply verify it doesn't throw and is idempotent
        _sut.CancelAll();
        _sut.CancelAll(); // Second call should not throw
    }

    [Fact]
    public void IsBusy_InitiallyFalse()
    {
        _sut.IsBusy.Should().BeFalse();
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ContainsModelName()
    {
        // The fixture SUT uses default brainModel parameter
        _sut.ProviderName.Should().Contain("OpenRouter");
        _sut.ProviderName.Should().Contain("claude-sonnet");
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CompletesChannel()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"x"},"finish_reason":"stop"}]}""");
        var sut = CreateServiceWithHandler(handler);

        sut.Dispose();

        sut.Results.TryRead(out _).Should().BeFalse();
        sut.Results.Completion.IsCompleted.Should().BeTrue();
    }

    // ── SubmitImageAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitImageAsync_Success_WritesImageAnalysisToChannel()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"Knight fork on e5"},"finish_reason":"stop"}]}""");
        using var sut = CreateServiceWithHandler(handler);

        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "analyze board");
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.ImageAnalysis);
        result.AnalysisText.Should().Be("Knight fork on e5");
        result.VoiceNarration.Should().Be("Knight fork on e5");
        result.Priority.Should().Be(BrainResultPriority.WhenIdle);
        result.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitImageAsync_HttpError_WritesErrorToChannel()
    {
        var handler = MockHttpHandler.FromJson(
            """{"error":{"message":"Server error"}}""",
            HttpStatusCode.InternalServerError);
        using var sut = CreateServiceWithHandler(handler);

        await sut.SubmitImageAsync(new byte[] { 1, 2, 3 }, "context");
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.Error);
        result.AnalysisText.Should().Contain("failed");
        result.Priority.Should().Be(BrainResultPriority.Silent);
    }

    [Fact]
    public async Task SubmitImageAsync_NullContent_DefaultsToFallbackText()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":null},"finish_reason":"stop"}]}""");
        using var sut = CreateServiceWithHandler(handler);

        await sut.SubmitImageAsync(new byte[] { 1 }, "ctx");
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.ImageAnalysis);
        result.AnalysisText.Should().Be("No analysis available");
    }

    [Fact]
    public async Task SubmitImageAsync_WithToolCalls_ExecutesMultiTurnLoop()
    {
        int callCount = 0;
        const string toolCallResponse =
            """{"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call_1","type":"function","function":{"name":"get_game_state","arguments":"{}"}}]},"finish_reason":"tool_calls"}]}""";
        const string finalResponse =
            """{"choices":[{"message":{"content":"After analysis: Nf3 is best"},"finish_reason":"stop"}]}""";

        var handler = new MockHttpHandler((_, _) =>
        {
            var json = Interlocked.Increment(ref callCount) == 1 ? toolCallResponse : finalResponse;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        });

        // Session must provide tool definitions for tools to be sent in request
        var sessionMock = CreateDefaultSessionMock();
        sessionMock.Setup(s => s.GetAvailableTools()).Returns(new List<ToolDefinition>
        {
            new()
            {
                Name = "get_game_state",
                Description = "Get current game state",
                ParametersSchema = """{"type":"object","properties":{}}"""
            }
        });

        using var sut = CreateServiceWithHandler(handler, sessionMock);
        await sut.SubmitImageAsync(new byte[] { 0x89, 0x50 }, "analyze board");
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.ImageAnalysis);
        result.AnalysisText.Should().Be("After analysis: Nf3 is best");
        callCount.Should().Be(2, "should make initial request + follow-up after tool execution");
    }

    // ── SubmitQueryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitQueryAsync_Success_WritesToolResultToChannel()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"Move your knight to f3"},"finish_reason":"stop"}]}""");
        using var sut = CreateServiceWithHandler(handler);
        var envelope = new SharedContextEnvelope
        {
            Intent = "tactical",
            RollingSummary = "Opponent controls center"
        };

        await sut.SubmitQueryAsync("What's the best move?", envelope);
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.ToolResult);
        result.AnalysisText.Should().Be("Move your knight to f3");
        result.VoiceNarration.Should().Be("Move your knight to f3");
        result.Priority.Should().Be(BrainResultPriority.WhenIdle);
        result.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitQueryAsync_HttpError_WritesErrorToChannel()
    {
        var handler = MockHttpHandler.FromJson(
            """{"error":{"message":"Rate limited"}}""",
            HttpStatusCode.TooManyRequests);
        using var sut = CreateServiceWithHandler(handler);
        var envelope = new SharedContextEnvelope { Intent = "general" };

        await sut.SubmitQueryAsync("query", envelope);
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.Error);
        result.AnalysisText.Should().Contain("failed");
        result.Priority.Should().Be(BrainResultPriority.Silent);
    }

    [Fact]
    public async Task SubmitQueryAsync_NullContent_DefaultsToFallbackText()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":null},"finish_reason":"stop"}]}""");
        using var sut = CreateServiceWithHandler(handler);
        var envelope = new SharedContextEnvelope { Intent = "general" };

        await sut.SubmitQueryAsync("query", envelope);
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.ToolResult);
        result.AnalysisText.Should().Be("No response available");
    }
}
