using System.Net;
using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Chess;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Brain;

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

        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var toolExecutor = new ToolExecutor(
            Mock.Of<IWindowCaptureService>(),
            _mockSession.Object,
            client,
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
        MockHttpHandler handler, Mock<ISessionManager>? sessionMock = null,
        TimeSpan? imageAnalysisMinInterval = null,
        int maxOpenRouterRetries = 4,
        TimeSpan? openRouterRetryBaseDelay = null)
    {
        sessionMock ??= CreateDefaultSessionMock();
        var httpClient = new HttpClient(handler);
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var toolExecutor = new ToolExecutor(
            Mock.Of<IWindowCaptureService>(),
            sessionMock.Object,
            client,
            new MockStockfishService(),
            "openai/gpt-4o-mini",
            mockLogger.Object);

        return new OpenRouterBrainService(
            client,
            toolExecutor,
            sessionMock.Object,
            imageAnalysisMinInterval: imageAnalysisMinInterval ?? TimeSpan.Zero,
            maxOpenRouterRetries: maxOpenRouterRetries,
            openRouterRetryBaseDelay: openRouterRetryBaseDelay ?? TimeSpan.Zero);
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
        // The fixture SUT uses the service default brain model.
        _sut.ProviderName.Should().Contain("OpenRouter");
        _sut.ProviderName.Should().Contain("gemini");
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
        var callCount = 0;
        var handler = new MockHttpHandler((_, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Server error: raw upstream payload"}}""", System.Text.Encoding.UTF8, "application/json")
            });
        });
        using var sut = CreateServiceWithHandler(handler, maxOpenRouterRetries: 2, openRouterRetryBaseDelay: TimeSpan.Zero);

        await sut.SubmitImageAsync(new byte[] { 1, 2, 3 }, "context");
        var result = await ReadResultWithTimeout(sut);

        callCount.Should().Be(3);
        result.Type.Should().Be(BrainResultType.Error);
        result.AnalysisText.Should().Contain("Brain service is temporarily unavailable");
        result.AnalysisText.Should().NotContain("raw upstream payload");
        result.Priority.Should().Be(BrainResultPriority.Silent);
        result.RequestDisconnect.Should().BeTrue();
        result.ErrorFingerprint.Should().Be("openrouter:http_500");
        result.AttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task SubmitImageAsync_NonRetryableAuthError_WritesSanitizedErrorWithoutRetries()
    {
        var handler = MockHttpHandler.FromJson(
            """{"error":{"message":"bad key leaked upstream"}}""",
            HttpStatusCode.Unauthorized);
        using var sut = CreateServiceWithHandler(handler, maxOpenRouterRetries: 4, openRouterRetryBaseDelay: TimeSpan.Zero);

        await sut.SubmitImageAsync(new byte[] { 1, 2, 3 }, "context");
        var result = await ReadResultWithTimeout(sut);

        result.Type.Should().Be(BrainResultType.Error);
        result.AnalysisText.Should().Be("Brain authentication failed. Check the OpenRouter key, then reconnect.");
        result.AnalysisText.Should().NotContain("bad key leaked upstream");
        result.AttemptCount.Should().Be(1);
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
    public async Task ChatAsync_InGame_UsesLiveBoardObserverWording()
    {
        string? requestJson = null;
        var handler = new MockHttpHandler(async (request, _) =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"reply"},"finish_reason":"stop"}]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var sut = CreateServiceWithHandler(handler);
        var reply = await sut.ChatAsync("What is happening on the board?", Array.Empty<ChatMessage>());

        reply.Should().Be("reply");
        requestJson.Should().NotBeNull();
        requestJson.Should().Contain("during an active live game session");
        requestJson.Should().Contain("Do not claim you cannot see the board");
        requestJson.Should().NotContain("outside of a game session");
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

    [Fact]
    public async Task SubmitImageAsync_RespectsConfiguredCooldown_BetweenSequentialImageRequests()
    {
        var requestTimes = new List<DateTime>();
        var requestLock = new object();
        var handler = new MockHttpHandler(async (request, _) =>
        {
            lock (requestLock)
            {
                requestTimes.Add(DateTime.UtcNow);
            }

            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}]}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        using var sut = CreateServiceWithHandler(handler, imageAnalysisMinInterval: TimeSpan.FromMilliseconds(150));

        await sut.SubmitImageAsync(new byte[] { 1 }, "first");
        await sut.SubmitImageAsync(new byte[] { 2 }, "second");

        await ReadResultWithTimeout(sut);
        await ReadResultWithTimeout(sut);

        requestTimes.Should().HaveCount(2);
        var delta = requestTimes[1] - requestTimes[0];
        delta.Should().BeGreaterThan(TimeSpan.FromMilliseconds(120));
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
        result.AnalysisText.Should().Contain("rate-limited");
        result.Priority.Should().Be(BrainResultPriority.Silent);
        result.RequestDisconnect.Should().BeTrue();
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

    // ── Counter Bug Fix Tests ────────────────────────────────────────────────

    [Fact]
    public async Task SubmitImageAsync_CounterReturnsToZero_AfterPreCancelledToken()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"test"},"finish_reason":"stop"}]}""");
        using var sut = CreateServiceWithHandler(handler);

        // Create a pre-cancelled token
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        await sut.SubmitImageAsync(new byte[] { 1 }, "ctx", preCancelled.Token);

        // Wait for Task.Run to resolve (whether delegate runs or is skipped)
        await Task.Delay(500);

        sut.IsBusy.Should().BeFalse(
            "counter must return to 0 even when token is pre-cancelled — " +
            "THIS IS THE CRITICAL BUG if this fails");
    }

    [Fact]
    public async Task SubmitQueryAsync_CounterReturnsToZero_AfterPreCancelledToken()
    {
        var handler = MockHttpHandler.FromJson(
            """{"choices":[{"message":{"content":"test"},"finish_reason":"stop"}]}""");
        using var sut = CreateServiceWithHandler(handler);
        var envelope = new SharedContextEnvelope { Intent = "general" };

        // Create a pre-cancelled token
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        await sut.SubmitQueryAsync("query", envelope, preCancelled.Token);

        // Wait for Task.Run to resolve
        await Task.Delay(500);

        sut.IsBusy.Should().BeFalse(
            "counter must return to 0 even when token is pre-cancelled");
    }

    [Fact]
    public async Task SubmitQueryAsync_CounterReturnsToZero_AfterCancellation()
    {
        // Use a slow handler (2s delay) so we can cancel during processing
        var handler = new MockHttpHandler(async (_, ct) =>
        {
            await Task.Delay(2000, ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"test"},"finish_reason":"stop"}]}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });
        using var sut = CreateServiceWithHandler(handler);
        var envelope = new SharedContextEnvelope { Intent = "general" };

        using var cts = new CancellationTokenSource();
        await sut.SubmitQueryAsync("query", envelope, cts.Token);

        // Cancel immediately
        cts.Cancel();

        // Wait for cancellation to propagate
        await Task.Delay(500);

        sut.IsBusy.Should().BeFalse(
            "counter must return to 0 after cancellation during in-flight request");
    }
}
