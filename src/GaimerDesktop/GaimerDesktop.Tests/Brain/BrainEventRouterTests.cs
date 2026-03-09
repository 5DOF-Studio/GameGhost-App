using System.Threading.Channels;
using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;
using GaimerDesktop.Services;
using GaimerDesktop.Services.Conversation;

namespace GaimerDesktop.Tests.Brain;

public class BrainEventRouterTests
{
    private readonly Mock<ITimelineFeed> _mockTimeline;
    private readonly Mock<IConversationProvider> _mockVoice;
    private readonly Mock<IBrainContextService> _mockBrainContext;
    private string? _capturedTopStrip;

    public BrainEventRouterTests()
    {
        _mockTimeline = new Mock<ITimelineFeed>();
        _mockVoice = new Mock<IConversationProvider>();
        _mockBrainContext = new Mock<IBrainContextService>();

        _mockVoice.Setup(v => v.SendContextualUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private BrainEventRouter CreateSut(bool withVoice = false)
    {
        return new BrainEventRouter(
            _mockTimeline.Object,
            withVoice ? _mockVoice.Object : null,
            s => _capturedTopStrip = s,
            _mockBrainContext.Object);
    }

    private static BrainHint MakeHint(
        string signal = "sage",
        string urgency = "low",
        string summary = "Test hint",
        int evaluation = 0,
        int? evalDelta = null,
        string? suggestedMove = null) => new()
    {
        Signal = signal,
        Urgency = urgency,
        Summary = summary,
        Evaluation = evaluation,
        EvalDelta = evalDelta,
        SuggestedMove = suggestedMove
    };

    // ── Signal → EventOutputType mapping ────────────────────────────────────

    [Fact]
    public void OnBrainHint_DangerSignal_MapsToEventOutputTypeDanger()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "danger"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.Danger)));
    }

    [Fact]
    public void OnBrainHint_BlunderSignal_MapsToEventOutputTypeDanger()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "blunder"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.Danger)));
    }

    [Fact]
    public void OnBrainHint_OpportunitySignal_MapsToOpportunity()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "opportunity"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.Opportunity)));
    }

    [Fact]
    public void OnBrainHint_BrilliantSignal_MapsToOpportunity()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "brilliant"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.Opportunity)));
    }

    [Fact]
    public void OnBrainHint_SageSignal_MapsToSageAdvice()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "sage"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.SageAdvice)));
    }

    [Fact]
    public void OnBrainHint_AssessmentSignal_MapsToAssessment()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "assessment"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.Assessment)));
    }

    [Fact]
    public void OnBrainHint_DetectionSignal_MapsToDetection()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "detection"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.Detection)));
    }

    [Fact]
    public void OnBrainHint_UnknownSignal_DefaultsToSageAdvice()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "xyz"));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e => e.Type == EventOutputType.SageAdvice)));
    }

    // ── Timeline events ─────────────────────────────────────────────────────

    [Fact]
    public void OnImageAnalysis_AddsTimelineEvent()
    {
        var sut = CreateSut();
        sut.OnImageAnalysis("Board position analyzed");

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.ImageAnalysis)));
    }

    [Fact]
    public void OnBrainHint_AddsTimelineEvent_WithBrainMetadata()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(signal: "danger", urgency: "high", evaluation: -300, evalDelta: -150));

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Brain != null &&
            e.Brain.Signal == "danger" &&
            e.Brain.Urgency == "high" &&
            e.Brain.Evaluation == -300)));
    }

    // ── Voice forwarding ────────────────────────────────────────────────────

    [Fact]
    public void OnBrainHint_VoiceConnected_SendsUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true);

        sut.OnBrainHint(MakeHint());

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnBrainHint_VoiceDisconnected_SkipsVoice()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(false);
        var sut = CreateSut(withVoice: true);

        sut.OnBrainHint(MakeHint());

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── OnDirectMessage ─────────────────────────────────────────────────────

    [Fact]
    public void OnDirectMessage_AddsTwoTimelineEvents()
    {
        var sut = CreateSut();
        var userMsg = new ChatMessage { Role = MessageRole.User, Content = "What should I do?" };
        var brainMsg = new ChatMessage { Role = MessageRole.Assistant, Content = "Try Nf3" };

        sut.OnDirectMessage(userMsg, brainMsg);

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Role == MessageRole.User)), Times.Once);
        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Role == MessageRole.Assistant)), Times.Once);
    }

    // ── OnProactiveAlert ────────────────────────────────────────────────────

    [Fact]
    public void OnProactiveAlert_HighUrgency_ForwardsToVoice()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true);

        sut.OnProactiveAlert(MakeHint(urgency: "high"), "Critical position!");

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnProactiveAlert_LowUrgency_SkipsVoice()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true);

        sut.OnProactiveAlert(MakeHint(urgency: "low"), "Minor observation");

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── OnGeneralChat ───────────────────────────────────────────────────────

    [Fact]
    public void OnGeneralChat_AddsTimelineEvent()
    {
        var sut = CreateSut();
        sut.OnGeneralChat("Hello world");

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.GeneralChat)));
    }

    // ── OnError ─────────────────────────────────────────────────────────────

    [Fact]
    public void OnError_AddsSystemErrorTimelineEvent()
    {
        var sut = CreateSut();
        sut.OnError("Connection lost");

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.SystemError
                && e.Role == MessageRole.System
                && e.Summary == "Connection lost")));
    }

    // ── OnScreenCapture ─────────────────────────────────────────────────────

    [Fact]
    public void OnScreenCapture_CallsNewCapture()
    {
        var sut = CreateSut();
        sut.OnScreenCapture("screenshot-001.png", TimeSpan.FromMinutes(5), "manual");

        _mockTimeline.Verify(t => t.NewCapture("screenshot-001.png", TimeSpan.FromMinutes(5), "manual"));
    }

    [Fact]
    public void OnScreenCapture_InvokesTopStrip()
    {
        var sut = CreateSut();
        sut.OnScreenCapture("screenshot-001.png", TimeSpan.FromMinutes(5), "manual");

        _capturedTopStrip.Should().Contain("Analyzing capture at");
    }

    // ── TopStrip callbacks ──────────────────────────────────────────────────

    [Fact]
    public void OnBrainHint_InvokesTopStrip_WithSummary()
    {
        var sut = CreateSut();
        sut.OnBrainHint(MakeHint(summary: "Knight fork detected"));

        _capturedTopStrip.Should().Be("Knight fork detected");
    }

    [Fact]
    public void OnImageAnalysis_InvokesTopStrip()
    {
        var sut = CreateSut();
        sut.OnImageAnalysis("Position is balanced");

        _capturedTopStrip.Should().Be("Position is balanced");
    }

    // ── RouteBrainResult (Channel consumer dispatch) ────────────────────────

    private async Task RouteViaChannel(BrainEventRouter sut, BrainResult result)
    {
        var channel = Channel.CreateUnbounded<BrainResult>();
        sut.StartConsuming(channel.Reader, CancellationToken.None);
        await channel.Writer.WriteAsync(result);
        channel.Writer.Complete();
        // Give the consumer loop time to process
        await Task.Delay(100);
        sut.StopConsuming();
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_RoutesToOnImageAnalysis()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Board shows Sicilian Defense"
        };

        await RouteViaChannel(sut, result);

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.ImageAnalysis)), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_ProactiveAlert_RoutesToOnProactiveAlert()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            Hint = MakeHint(signal: "danger", urgency: "high"),
            AnalysisText = "Blunder detected"
        };

        await RouteViaChannel(sut, result);

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Role == MessageRole.Proactive)), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_ToolResult_RoutesToOnGeneralChat()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "Engine analysis: +0.35 e2e4"
        };

        await RouteViaChannel(sut, result);

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.GeneralChat)), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_Error_RoutesToTimelineAsSystemError()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = "API timeout"
        };

        await RouteViaChannel(sut, result);

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.SystemError
                && e.Summary == "API timeout"
                && e.Role == MessageRole.System)), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_WithAnalysisText_IngestsL1Event()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position is equal"
        };

        await RouteViaChannel(sut, result);

        _mockBrainContext.Verify(c => c.IngestEventAsync(
            It.Is<BrainEvent>(e =>
                e.Type == BrainEventType.VisionObservation &&
                e.Category == "vision" &&
                e.Text == "Position is equal"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_NullAnalysisText_SkipsL1Ingestion()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = null
        };

        await RouteViaChannel(sut, result);

        _mockBrainContext.Verify(c => c.IngestEventAsync(
            It.IsAny<BrainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RouteBrainResult_InterruptPriority_ForwardsUrgentToVoice()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true);
        var result = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            Hint = MakeHint(signal: "blunder", urgency: "high"),
            AnalysisText = "Blunder!",
            VoiceNarration = "You just blundered!",
            Priority = BrainResultPriority.Interrupt
        };

        await RouteViaChannel(sut, result);

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.Is<string>(s => s.Contains("[URGENT]")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_SilentPriority_SkipsVoice()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed",
            VoiceNarration = "Something to say",
            Priority = BrainResultPriority.Silent
        };

        await RouteViaChannel(sut, result);

        // Silent priority: no voice call from RouteBrainResult path
        // (OnImageAnalysis doesn't call voice; and Silent skips voice forwarding)
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RouteBrainResult_ToolResult_L1IngestCategoryIsTool()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "Best move: e2e4 (+0.35)"
        };

        await RouteViaChannel(sut, result);

        _mockBrainContext.Verify(c => c.IngestEventAsync(
            It.Is<BrainEvent>(e =>
                e.Type == BrainEventType.GameplayState &&
                e.Category == "tool"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
