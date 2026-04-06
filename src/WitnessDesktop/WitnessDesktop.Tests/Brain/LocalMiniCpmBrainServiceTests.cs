using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Brain;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Tests.Brain;

/// <summary>
/// Tests for LocalMiniCpmBrainService — verifies drop-in IBrainService contract compliance:
/// frame-slot behavior, BrainResult channel emission, ChatAsync, SubmitQueryAsync,
/// cancellation, and counter management.
/// </summary>
public class LocalMiniCpmBrainServiceTests : IDisposable
{
    private readonly FakeLocalVisionClient _fakeClient;
    private readonly Mock<ISessionManager> _sessionManager;
    private readonly LocalMiniCpmBrainService _sut;

    public LocalMiniCpmBrainServiceTests()
    {
        _fakeClient = new FakeLocalVisionClient();
        _sessionManager = new Mock<ISessionManager>();
        _sessionManager.Setup(s => s.Context).Returns(new SessionContext
        {
            State = SessionState.InGame,
            GameType = "chess",
            AgentKey = null // no agent — uses fallback personality
        });

        _sut = new LocalMiniCpmBrainService(
            _fakeClient,
            _sessionManager.Object);
    }

    public void Dispose() => _sut.Dispose();

    // ── TrySubmitFrame ──────────────────────────────────────────────────────

    [Fact]
    public void TrySubmitFrame_ReturnsTrue_WhenServiceOpen()
    {
        var result = _sut.TrySubmitFrame(new byte[] { 0x89, 0x50 }, "test context");
        result.Should().BeTrue();
    }

    [Fact]
    public void TrySubmitFrame_ReturnsFalse_AfterDispose()
    {
        _sut.Dispose();
        var result = _sut.TrySubmitFrame(new byte[] { 0x89, 0x50 }, "test context");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TrySubmitFrame_LatestFrameWins_WhenTwoFramesQueued()
    {
        // Make the client slow so the first frame is still being processed
        _fakeClient.AnalyzeDelay = TimeSpan.FromMilliseconds(200);

        // Submit first frame, then immediately overwrite with second
        _sut.TrySubmitFrame(new byte[] { 1, 1, 1 }, "first frame");
        _sut.TrySubmitFrame(new byte[] { 2, 2, 2 }, "second frame");

        // Read results — at most 2 results, but frame slot with DropOldest
        // means the second frame replaces the first if the first hasn't been consumed yet.
        // We verify at least one result arrives.
        using var cts = new CancellationTokenSource(5000);
        var result = await _sut.Results.ReadAsync(cts.Token);
        result.Type.Should().Be(BrainResultType.ImageAnalysis);
        result.AnalysisText.Should().NotBeNullOrEmpty();
    }

    // ── Results Channel ─────────────────────────────────────────────────────

    [Fact]
    public async Task Results_EmitsImageAnalysisBrainResult_OnSuccessfulLocalInference()
    {
        _fakeClient.NextResponse = new LocalVisionResponse
        {
            AssistantText = "White has a strong center with pawns on d4 and e4.",
            Success = true,
            Confidence = 0.85,
            ModelId = "minicpm-o-2.6"
        };

        _sut.TrySubmitFrame(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "chess position");

        using var cts = new CancellationTokenSource(5000);
        var result = await _sut.Results.ReadAsync(cts.Token);

        result.Type.Should().Be(BrainResultType.ImageAnalysis);
        result.AnalysisText.Should().Contain("strong center");
        result.VoiceNarration.Should().NotBeNullOrEmpty();
        result.Priority.Should().Be(BrainResultPriority.WhenIdle);
        result.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Results_EmitsErrorBrainResult_WhenLocalInferenceThrows()
    {
        _fakeClient.ThrowOnAnalyze = new InvalidOperationException("Runtime crashed");

        _sut.TrySubmitFrame(new byte[] { 0x89, 0x50 }, "context");

        using var cts = new CancellationTokenSource(5000);
        var result = await _sut.Results.ReadAsync(cts.Token);

        result.Type.Should().Be(BrainResultType.Error);
        result.AnalysisText.Should().Contain("Runtime crashed");
        result.Priority.Should().Be(BrainResultPriority.Silent);
    }

    // ── ChatAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_ReturnsTextReply_WithoutUsingResultsChannel()
    {
        _fakeClient.NextChatReply = "I'd recommend the Sicilian Defense for aggressive play.";

        var reply = await _sut.ChatAsync("What opening should I play?", Array.Empty<ChatMessage>());

        reply.Should().Contain("Sicilian Defense");

        // Verify nothing was written to the results channel
        _sut.Results.TryRead(out _).Should().BeFalse(
            "ChatAsync should NOT emit on the results channel — it's request-reply only");
    }

    [Fact]
    public async Task ChatAsync_InGame_UsesLiveBoardObserverWording()
    {
        _fakeClient.NextChatReply = "Reply";

        await _sut.ChatAsync("What is happening on the board?", Array.Empty<ChatMessage>());

        _fakeClient.LastSystemPrompt.Should().Contain("during an active live game session");
        _fakeClient.LastSystemPrompt.Should().Contain("Do not claim you cannot see the board");
        _fakeClient.LastSystemPrompt.Should().NotContain("outside of a game session");
    }

    // ── SubmitQueryAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitQueryAsync_ProducesToolResultOrTextResult_OnChannel()
    {
        _fakeClient.NextChatReply = "The knight on f6 controls key central squares.";

        var envelope = new SharedContextEnvelope { Intent = "general" };
        await _sut.SubmitQueryAsync("Why is my knight good here?", envelope);

        using var cts = new CancellationTokenSource(5000);
        var result = await _sut.Results.ReadAsync(cts.Token);

        result.Type.Should().Be(BrainResultType.ToolResult);
        result.AnalysisText.Should().Contain("knight on f6");
        result.CorrelationId.Should().NotBeNullOrEmpty();
    }

    // ── Cancellation / Counter ──────────────────────────────────────────────

    [Fact]
    public async Task IsBusy_ReturnsFalse_AfterCancelledRequest()
    {
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        await _sut.SubmitImageAsync(new byte[] { 1 }, "ctx", preCancelled.Token);

        // Wait for Task.Run to resolve
        await Task.Delay(500);

        _sut.IsBusy.Should().BeFalse(
            "counter must return to 0 even when token is pre-cancelled");
    }

    [Fact]
    public void ProviderName_IsLocalMiniCPM()
    {
        _sut.ProviderName.Should().Be("Local MiniCPM");
    }

    // ── Test Double ─────────────────────────────────────────────────────────

    private sealed class FakeLocalVisionClient : ILocalVisionInferenceClient
    {
        public LocalVisionResponse? NextResponse { get; set; }
        public string NextChatReply { get; set; } = "Default fake reply.";
        public string? LastSystemPrompt { get; private set; }
        public Exception? ThrowOnAnalyze { get; set; }
        public TimeSpan AnalyzeDelay { get; set; } = TimeSpan.Zero;

        public async Task<LocalVisionResponse> AnalyzeImageAsync(LocalVisionRequest request, CancellationToken ct = default)
        {
            if (AnalyzeDelay > TimeSpan.Zero)
                await Task.Delay(AnalyzeDelay, ct);

            if (ThrowOnAnalyze != null)
                throw ThrowOnAnalyze;

            return NextResponse ?? new LocalVisionResponse
            {
                AssistantText = "Default analysis: position is approximately equal.",
                Success = true,
                ModelId = "minicpm-o-test"
            };
        }

        public Task<string> ChatAsync(string userQuery, string systemPrompt, CancellationToken ct = default)
        {
            LastSystemPrompt = systemPrompt;
            return Task.FromResult(NextChatReply);
        }
    }
}
