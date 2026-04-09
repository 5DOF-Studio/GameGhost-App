using System.Threading.Channels;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;
using WitnessDesktop.Services.History;

namespace WitnessDesktop.Tests.Brain;

public class BrainEventRouterTests
{
    private readonly Mock<ITimelineFeed> _mockTimeline;
    private readonly Mock<IConversationProvider> _mockVoice;
    private readonly Mock<IBrainContextService> _mockBrainContext;
    private readonly Mock<IGameJournalService> _mockJournal;
    private readonly Mock<ISessionHistoryService> _mockHistory;
    private readonly Mock<ISessionTraceService> _mockSessionTrace;
    private readonly Mock<IVoiceGroundingCoordinator> _mockVoiceGrounding;
    private string? _capturedTopStrip;

    public BrainEventRouterTests()
    {
        _mockTimeline = new Mock<ITimelineFeed>();
        _mockVoice = new Mock<IConversationProvider>();
        _mockBrainContext = new Mock<IBrainContextService>();
        _mockJournal = new Mock<IGameJournalService>();
        _mockHistory = new Mock<ISessionHistoryService>();
        _mockSessionTrace = new Mock<ISessionTraceService>();
        _mockVoiceGrounding = new Mock<IVoiceGroundingCoordinator>();

        _mockVoice.Setup(v => v.SendContextualUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockVoice.Setup(v => v.SendContextualUpdateWithResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockVoiceGrounding.Setup(v => v.GetGroundingPrefix()).Returns("[GROUNDED BOARD STATE: White is slightly better]");

        // Default: validation passes (tests override as needed)
        _mockJournal.Setup(j => j.ValidateTemporalConsistency(It.IsAny<string?>()))
            .Returns(new TemporalValidation(true, null, "Consistent"));
        _mockHistory.Setup(h => h.PersistTimelineEventAsync(
                It.IsAny<string>(),
                It.IsAny<TimelineEvent>(),
                It.IsAny<string?>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _mockSessionTrace.SetupGet(t => t.SessionId).Returns("session-replay-1");
    }

    private BrainEventRouter CreateSut(bool withVoice = false, bool withJournal = false, bool withGrounding = false)
    {
        return new BrainEventRouter(
            _mockTimeline.Object,
            withVoice ? _mockVoice.Object : null,
            s => _capturedTopStrip = s,
            _mockBrainContext.Object,
            gameJournal: withJournal ? _mockJournal.Object : null,
            voiceGrounding: withGrounding ? _mockVoiceGrounding.Object : null,
            historyService: _mockHistory.Object,
            sessionTrace: _mockSessionTrace.Object);
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
    public void OnImageAnalysis_SkipsTimeline_UpdatesTopStrip()
    {
        var sut = CreateSut();
        sut.OnImageAnalysis("Board position analyzed");

        // D-039: ImageAnalysis no longer goes to timeline
        _mockTimeline.Verify(t => t.AddEvent(
            It.IsAny<TimelineEvent>()), Times.Never);
        _capturedTopStrip.Should().Be("Board position analyzed");
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
    public void OnBrainHint_WithGrounding_PrefixesVoiceUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true, withGrounding: true);

        sut.OnBrainHint(MakeHint(summary: "Knight fork detected"));

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.Is<string>(s => s.Contains("[GROUNDED BOARD STATE: White is slightly better]") &&
                               s.Contains("[BRAIN SIGNAL]")),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public void OnProactiveAlert_HighUrgency_WithGrounding_PrefixesVoiceUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true, withGrounding: true);

        sut.OnProactiveAlert(MakeHint(urgency: "high"), "Critical position!");

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.Is<string>(s => s.Contains("[GROUNDED BOARD STATE: White is slightly better]") &&
                               s.Contains("[BRAIN SIGNAL]")),
            It.IsAny<CancellationToken>()), Times.Once);
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
        await Task.Delay(250);
        sut.StopConsuming();
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_UpdatesTopStrip()
    {
        var sut = CreateSut();
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Board shows Sicilian Defense"
        };

        await RouteViaChannel(sut, result);

        _capturedTopStrip.Should().Contain("Board shows Sicilian Defense");
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

        _mockVoice.Verify(v => v.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s => s.Contains("[URGENT]")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_InterruptPriority_WithGrounding_PrefixesVoiceUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true, withGrounding: true);
        var result = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            Hint = MakeHint(signal: "blunder", urgency: "high"),
            AnalysisText = "Blunder!",
            VoiceNarration = "You just blundered!",
            Priority = BrainResultPriority.Interrupt
        };

        await RouteViaChannel(sut, result);

        _mockVoice.Verify(v => v.SendContextualUpdateWithResponseAsync(
            It.Is<string>(s => s.Contains("[GROUNDED BOARD STATE: White is slightly better]") &&
                               s.Contains("[URGENT] You just blundered!")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_WhenIdle_WithGrounding_PrefixesVoiceUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true, withGrounding: true);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed",
            VoiceNarration = "White is slightly better here.",
            Priority = BrainResultPriority.WhenIdle
        };

        await RouteViaChannel(sut, result);

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.Is<string>(s => s.Contains("[GROUNDED BOARD STATE: White is slightly better]") &&
                               s.Contains("White is slightly better here.")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_WhenIdle_WithDuplicateGroundingWithinCooldown_SuppressesSecondVoiceUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true, withGrounding: true);

        var first = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed",
            VoiceNarration = "White is slightly better here.",
            Priority = BrainResultPriority.WhenIdle
        };

        var second = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed again",
            VoiceNarration = "White still has the edge.",
            Priority = BrainResultPriority.WhenIdle
        };

        await RouteViaChannel(sut, first);
        await RouteViaChannel(sut, second);

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_InterruptPriority_DoesNotSuppressVoiceUpdate()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        var sut = CreateSut(withVoice: true, withGrounding: true);

        var idle = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed",
            VoiceNarration = "White is slightly better here.",
            Priority = BrainResultPriority.WhenIdle
        };

        var urgent = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            Hint = MakeHint(signal: "danger", urgency: "high"),
            AnalysisText = "Tactical shot",
            VoiceNarration = "Watch out for the tactic now.",
            Priority = BrainResultPriority.Interrupt
        };

        await RouteViaChannel(sut, idle);
        await RouteViaChannel(sut, urgent);

        // Idle uses non-response SendContextualUpdateAsync (1 call)
        // ProactiveAlert's OnProactiveAlert also calls SendContextualUpdateAsync (1 call)
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Interrupt priority uses response-triggering variant (1 call)
        _mockVoice.Verify(v => v.SendContextualUpdateWithResponseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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

    // ── Journal ingestion ────────────────────────────────────────────────────

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_WithJournal_AddsJournalEntry()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "White has a strong center with pawns on e4 and d4"
        };

        await RouteViaChannel(sut, result);

        _mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.MoveNumber == 1 &&
            e.Description.Length <= 200 &&
            e.MoveNotation == null)),
            Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_WithFenInText_ExtractsFen()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = $"The current position is {fen}. White has played e4."
        };

        await RouteViaChannel(sut, result);

        _mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.Fen == fen)),
            Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_WithoutFen_SetsNullFen()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "The board shows a standard opening"
        };

        await RouteViaChannel(sut, result);

        _mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.Fen == null)),
            Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_ToolResult_DoesNotAddJournalEntry()
    {
        var sut = CreateSut(withJournal: true);
        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "Engine analysis result"
        };

        await RouteViaChannel(sut, result);

        _mockJournal.Verify(j => j.AddEntry(It.IsAny<GameJournalEntry>()), Times.Never);
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_WithoutJournal_DoesNotThrow()
    {
        var sut = CreateSut(withJournal: false);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Position analyzed"
        };

        // Should not throw even without journal service
        await RouteViaChannel(sut, result);

        // D-039: unstructured ImageAnalysis no longer goes to timeline, only TopStrip
        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.ImageAnalysis)), Times.Never);
        _capturedTopStrip.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_TruncatesLongDescription()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        var longText = new string('A', 300);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = longText
        };

        await RouteViaChannel(sut, result);

        _mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.Description.Length == 200)),
            Times.Once);
    }

    // ── Structured JSON parsing in RouteBrainResult ─────────────────────────

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_WithStructuredJson_ParsesDisplayText()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        var json = """{"visual_description":"Board shows e4","position_assessment":"White controls center","confidence":"LIKELY","fen":"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1","threats":"None","suggested_action":"Develop pieces"}""";
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = json
        };

        await RouteViaChannel(sut, result);
        while (sut.EmitNextQueued()) { } // D-040: drain emission queue

        // D-039: ImageAnalysis no longer emitted to timeline
        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.ImageAnalysis)), Times.Never);
        // Assessment still emitted
        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e =>
                e.Type == EventOutputType.Assessment &&
                e.FullContent != null &&
                e.FullContent.Contains("White controls center"))));

        // Journal should get structured FEN
        _mockJournal.Verify(j => j.AddEntry(It.Is<GameJournalEntry>(e =>
            e.Fen != null &&
            e.Fen.Contains("rnbqkbnr"))));
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_WithFreeText_StillWorks()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "White has a strong center with pawns on e4 and d4"
        };

        await RouteViaChannel(sut, result);

        // D-039: unstructured ImageAnalysis no longer goes to timeline, only TopStrip
        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.ImageAnalysis)), Times.Never);
        _capturedTopStrip.Should().NotBeNullOrEmpty();
    }

    // ── Temporal consistency validation in journal ingestion ─────────────────

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_CallsValidateTemporalConsistency()
    {
        var sut = CreateSut(withJournal: true);
        _mockJournal.Setup(j => j.EntryCount).Returns(0);
        _mockJournal.Setup(j => j.ValidateTemporalConsistency(It.IsAny<string?>()))
            .Returns(new TemporalValidation(true, null, "Consistent"));
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = $"Position: [FEN: {fen}]"
        };

        await RouteViaChannel(sut, result);

        _mockJournal.Verify(j => j.ValidateTemporalConsistency(fen), Times.Once);
    }

    [Fact]
    public async Task RouteBrainResult_ImageAnalysis_InconsistentEntry_StillRecords()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var sut = new BrainEventRouter(
            _mockTimeline.Object,
            null,
            null,
            _mockBrainContext.Object,
            mockTelemetry.Object,
            _mockJournal.Object);

        _mockJournal.Setup(j => j.EntryCount).Returns(1);
        _mockJournal.Setup(j => j.ValidateTemporalConsistency(It.IsAny<string?>()))
            .Returns(new TemporalValidation(false, "DUPLICATE_POSITION", "Duplicate FEN"));

        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = $"Position: [FEN: {fen}]"
        };

        await RouteViaChannel(sut, result);

        // Entry should still be added despite inconsistency (no data loss)
        _mockJournal.Verify(j => j.AddEntry(It.IsAny<GameJournalEntry>()), Times.Once);

        // Telemetry should track the inconsistency
        mockTelemetry.Verify(t => t.TrackEvent("journal", "temporal_inconsistency",
            It.Is<Dictionary<string, string>>(d =>
                d["warning"] == "DUPLICATE_POSITION")), Times.Once);
    }

    // ── Tool Call Visibility (T16) ──────────────────────────────────────────

    [Fact]
    public void OnToolCall_EmitsToolCallTimelineEvent()
    {
        var sut = CreateSut();
        var toolCall = new ToolCallInfo
        {
            ToolName = "analyze_position_engine",
            Success = true,
            DurationMs = 42
        };

        sut.OnToolCall(toolCall);

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Type == EventOutputType.ToolCall &&
            e.Summary == "Ran engine analysis" &&
            e.ToolCall == toolCall
        )), Times.Once);
    }

    [Fact]
    public void OnToolCall_UsesToolDefinitionIcon()
    {
        var sut = CreateSut();
        var toolCall = new ToolCallInfo
        {
            ToolName = "web_search",
            Success = true
        };

        sut.OnToolCall(toolCall);

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Icon == "tool_search.svg"
        )), Times.Once);
    }

    [Fact]
    public void OnToolCall_FailedTool_ShowsFailureSummary()
    {
        var sut = CreateSut();
        var toolCall = new ToolCallInfo
        {
            ToolName = "get_game_state",
            Success = false
        };

        sut.OnToolCall(toolCall);

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Summary == "Game State failed"
        )), Times.Once);
    }

    [Fact]
    public void OnToolCall_WithDuration_IncludesDurationInFullContent()
    {
        var sut = CreateSut();
        var toolCall = new ToolCallInfo
        {
            ToolName = "analyze_position_engine",
            Success = true,
            DurationMs = 150
        };

        sut.OnToolCall(toolCall);

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.FullContent!.Contains("150ms")
        )), Times.Once);
    }

    [Fact]
    public void ToolCallInfo_DisplayName_ResolvesFromDefinition()
    {
        var tc = new ToolCallInfo { ToolName = "capture_screen", Success = true };
        tc.DisplayName.Should().Be("Capture Screen");
        tc.ActionLabel.Should().Be("Captured screen");
        tc.Icon.Should().Be("tool_capture.png");
    }

    [Fact]
    public void ToolCallInfo_UnknownTool_FallsBackToName()
    {
        var tc = new ToolCallInfo { ToolName = "unknown_tool", Success = true };
        tc.DisplayName.Should().Be("unknown_tool");
        tc.SummaryText.Should().Be("unknown_tool");
    }

    [Fact]
    public void OnToolCall_FiresToolCallReceivedEvent()
    {
        var sut = CreateSut();
        ToolCallInfo? received = null;
        sut.ToolCallReceived += tc => received = tc;

        var toolCall = new ToolCallInfo
        {
            ToolName = "web_search",
            Success = true
        };

        sut.OnToolCall(toolCall);

        received.Should().BeSameAs(toolCall);
    }

    [Fact]
    public void OnToolCall_ShowReplay_RoutesVideoCardAndPersistsIt()
    {
        var sut = CreateSut();
        ToolCallInfo? received = null;
        sut.ToolCallReceived += tc => received = tc;

        var toolCall = new ToolCallInfo
        {
            ToolName = "show_replay",
            Success = true,
            OutputJson = """
            {
              "status": "success",
              "filePath": "/tmp/replays/segment-0.mp4",
              "startTime": 60.0,
              "duration": 30.0,
              "title": "WATCH THIS"
            }
            """
        };

        sut.OnToolCall(toolCall);

        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Type == EventOutputType.ToolCall)), Times.Never);
        _mockTimeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Type == EventOutputType.VideoCard &&
            e.Media != null &&
            e.Media.FilePath == "/tmp/replays/segment-0.mp4" &&
            e.Media.StartTime == 60.0 &&
            e.Media.Duration == 30.0 &&
            e.Media.Title == "WATCH THIS")), Times.Once);
        _mockHistory.Verify(h => h.PersistTimelineEventAsync(
            "session-replay-1",
            It.Is<TimelineEvent>(e =>
                e.Type == EventOutputType.VideoCard &&
                e.Media != null &&
                e.Media.FilePath == "/tmp/replays/segment-0.mp4"),
            null,
            0), Times.Once);
        received.Should().BeSameAs(toolCall);
    }

    // ── SlidingPanelContent Tool Layout (Ghost Card) ────────────────────────

    [Fact]
    public void SlidingPanelContent_WithToolCall_IsToolCallTrue()
    {
        var content = new SlidingPanelContent
        {
            Title = "TOOL",
            Text = "Ran engine analysis",
            ToolCall = new ToolCallInfo { ToolName = "analyze_position_engine", Success = true }
        };

        content.IsToolCall.Should().BeTrue();
        content.ToolIconPath.Should().Be("tool_engine.png");
    }

    [Fact]
    public void SlidingPanelContent_WithoutToolCall_IsToolCallFalse()
    {
        var content = new SlidingPanelContent
        {
            Title = "AI INSIGHT",
            Text = "Some analysis"
        };

        content.IsToolCall.Should().BeFalse();
        content.ToolIconPath.Should().Be("tool_generic.png");
    }
}

// ==========================================================================
// STRUCTURED ANALYSIS DISTRIBUTION
// ==========================================================================

public class BrainEventRouter_StructuredAnalysisTests
{
    private readonly Mock<ITimelineFeed> _mockTimeline;
    private readonly List<TimelineEvent> _capturedEvents = new();
    private string? _capturedTopStrip;

    public BrainEventRouter_StructuredAnalysisTests()
    {
        _mockTimeline = new Mock<ITimelineFeed>();
        _mockTimeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => _capturedEvents.Add(e));
    }

    private BrainEventRouter CreateSut()
    {
        return new BrainEventRouter(
            _mockTimeline.Object,
            topStrip: s => _capturedTopStrip = s);
    }

    [Fact]
    public void OnStructuredAnalysis_ImageAnalysis_NeverAddedToTimeline()
    {
        var sut = CreateSut();
        var analysis = new BrainAnalysisResult
        {
            VisualDescription = "A chess board with pieces",
            PositionAssessment = "White is ahead",
            Threats = "Knight fork",
            SuggestedAction = "Take the pawn",
        };

        sut.OnStructuredAnalysis(analysis);
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().NotContain(e => e.Type == EventOutputType.ImageAnalysis);
    }

    [Fact]
    public void OnImageAnalysis_SkipsTimeline_OnlyUpdatesTopStrip()
    {
        var sut = CreateSut();

        sut.OnImageAnalysis("Board has pieces in starting position");

        _capturedEvents.Should().BeEmpty();
        _capturedTopStrip.Should().Be("Board has pieces in starting position");
    }

    [Fact]
    public void OnStructuredAnalysis_AllSections_EmitsThreeEvents_ExcludesImageAnalysis()
    {
        var sut = CreateSut();
        var analysis = new BrainAnalysisResult
        {
            VisualDescription = "A chess board with pieces in play",
            PositionAssessment = "White has a slight advantage",
            Threats = "Black's knight threatens the queen",
            SuggestedAction = "Move queen to d3",
            Confidence = "LIKELY",
            Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
        };

        sut.OnStructuredAnalysis(analysis);
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().HaveCount(3);
        _capturedEvents[0].Type.Should().Be(EventOutputType.Danger);
        _capturedEvents[0].FullContent.Should().Be("Black's knight threatens the queen");
        _capturedEvents[1].Type.Should().Be(EventOutputType.Assessment);
        _capturedEvents[1].FullContent.Should().Be("White has a slight advantage");
        _capturedEvents[2].Type.Should().Be(EventOutputType.SageAdvice);
        _capturedEvents[2].FullContent.Should().Be("Move queen to d3");
    }

    [Fact]
    public void OnStructuredAnalysis_MissingSections_EmitsOnlyPresent()
    {
        var sut = CreateSut();
        var analysis = new BrainAnalysisResult
        {
            VisualDescription = "A chess board",
            PositionAssessment = null,
            Threats = "Knight fork",
            SuggestedAction = null,
        };

        sut.OnStructuredAnalysis(analysis);
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().HaveCount(1);
        _capturedEvents[0].Type.Should().Be(EventOutputType.Danger);
    }

    [Fact]
    public void OnStructuredAnalysis_EmptySections_SkipsWhitespace()
    {
        var sut = CreateSut();
        var analysis = new BrainAnalysisResult
        {
            VisualDescription = "  ",
            PositionAssessment = "",
            Threats = null,
            SuggestedAction = "Take the pawn",
        };

        sut.OnStructuredAnalysis(analysis);
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().HaveCount(1);
        _capturedEvents[0].Type.Should().Be(EventOutputType.SageAdvice);
    }

    [Fact]
    public void OnStructuredAnalysis_EventsHaveCorrectIcons()
    {
        var sut = CreateSut();
        var analysis = new BrainAnalysisResult
        {
            VisualDescription = "Board view",
            PositionAssessment = "Even position",
            Threats = "Back rank mate threat",
            SuggestedAction = "Castle kingside",
        };

        sut.OnStructuredAnalysis(analysis);
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().HaveCount(3);
        _capturedEvents[0].Icon.Should().Be(EventIconMap.GetIcon(EventOutputType.Danger));
        _capturedEvents[1].Icon.Should().Be(EventIconMap.GetIcon(EventOutputType.Assessment));
        _capturedEvents[2].Icon.Should().Be(EventIconMap.GetIcon(EventOutputType.SageAdvice));
    }

    [Fact]
    public void RouteBrainResult_StructuredJson_EmitsMultipleEvents()
    {
        var sut = CreateSut();
        var json = """{"visual_description":"Board","position_assessment":"Even","threats":"Fork","suggested_action":"Defend"}""";

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = json,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().HaveCount(3);
        _capturedEvents[0].Type.Should().Be(EventOutputType.Danger);
        _capturedEvents[1].Type.Should().Be(EventOutputType.Assessment);
        _capturedEvents[2].Type.Should().Be(EventOutputType.SageAdvice);
    }

    [Fact]
    public void RouteBrainResult_UnstructuredText_FallsBackToSingleEvent()
    {
        var sut = CreateSut();

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "This is plain unstructured analysis text.",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _capturedEvents.Should().BeEmpty();
        _capturedTopStrip.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RouteBrainResult_StructuredJson_TopStripGetsPositionAssessment()
    {
        var sut = CreateSut();
        var json = """{"visual_description":"Board","position_assessment":"White is winning","threats":"None","suggested_action":"Push pawns"}""";

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = json,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _capturedTopStrip.Should().Be("White is winning");
    }

    // ── BUG-011: Markdown labeled text recovery via RouteBrainResult ────────

    [Fact]
    public void RouteBrainResult_MarkdownStructuredText_EmitsMultipleEvents()
    {
        var sut = CreateSut();
        var markdown = "**VISUAL DESCRIPTION:** Board shows Sicilian Defense.\n**POSITION ASSESSMENT:** White is slightly better.\n**THREATS:** Knight fork on f7.\n**SUGGESTED ACTION:** Develop Nf3.";

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = markdown,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        while (sut.EmitNextQueued()) { }

        _capturedEvents.Should().HaveCount(3);
        _capturedEvents[0].Type.Should().Be(EventOutputType.Danger);
        _capturedEvents[0].FullContent.Should().Contain("Knight fork");
        _capturedEvents[1].Type.Should().Be(EventOutputType.Assessment);
        _capturedEvents[2].Type.Should().Be(EventOutputType.SageAdvice);
    }

    [Fact]
    public void RouteBrainResult_MarkdownStructuredText_TopStripUsesAssessment()
    {
        var sut = CreateSut();
        var markdown = "**VISUAL DESCRIPTION:** Board.\n**POSITION ASSESSMENT:** White is dominating.\n**THREATS:** None.";

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = markdown,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _capturedTopStrip.Should().Contain("White is dominating");
    }

    [Fact]
    public void RouteBrainResult_UnstructuredFallback_SanitizesAsterisks()
    {
        var sut = CreateSut();
        // Only one labeled section — not enough for heuristic recovery
        var dirtyText = "**VISUAL DESCRIPTION:** unclear board state with **bold** markers";

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = dirtyText,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _capturedEvents.Should().BeEmpty();
        _capturedTopStrip.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RouteBrainResult_UnstructuredFallback_TopStripSanitized()
    {
        var sut = CreateSut();
        var dirtyText = "**VISUAL DESCRIPTION:** just a single dirty section here";

        sut.RouteBrainResultForTest(new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = dirtyText,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _capturedTopStrip.Should().NotContain("**");
        _capturedTopStrip.Should().NotContain("VISUAL DESCRIPTION:");
    }
}

// ==========================================================================
// EMISSION QUEUE (D-040)
// ==========================================================================

public class BrainEventRouter_EmissionQueueTests
{
    private readonly Mock<ITimelineFeed> _mockTimeline;
    private readonly List<TimelineEvent> _capturedEvents = new();

    public BrainEventRouter_EmissionQueueTests()
    {
        _mockTimeline = new Mock<ITimelineFeed>();
        _mockTimeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => _capturedEvents.Add(e));
    }

    private BrainEventRouter CreateSut() =>
        new BrainEventRouter(_mockTimeline.Object);

    [Fact]
    public void EnqueueAnalysis_ThenDrain_EmitsInPriorityOrder()
    {
        var sut = CreateSut();
        var analysis = new BrainAnalysisResult
        {
            VisualDescription = "Board view",
            PositionAssessment = "Even position",
            Threats = "Back rank mate",
            SuggestedAction = "Castle kingside",
        };

        sut.OnStructuredAnalysis(analysis);

        // Nothing emitted yet — events are queued
        _capturedEvents.Should().BeEmpty();

        // Drain one at a time
        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents.Should().HaveCount(1);
        _capturedEvents[0].Type.Should().Be(EventOutputType.Danger);

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents.Should().HaveCount(2);
        _capturedEvents[1].Type.Should().Be(EventOutputType.Assessment);

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents.Should().HaveCount(3);
        _capturedEvents[2].Type.Should().Be(EventOutputType.SageAdvice);

        // Queue empty
        sut.EmitNextQueued().Should().BeFalse();
    }

    [Fact]
    public void NewBatch_PreservesPendingQueue()
    {
        var sut = CreateSut();

        sut.OnStructuredAnalysis(new BrainAnalysisResult
        {
            Threats = "Old threat",
            PositionAssessment = "Old assessment",
            SuggestedAction = "Old advice",
        });

        sut.EmitNextQueued();
        _capturedEvents.Should().HaveCount(1);
        _capturedEvents[0].FullContent.Should().Be("Old threat");

        sut.OnStructuredAnalysis(new BrainAnalysisResult
        {
            Threats = "New threat",
            SuggestedAction = "New advice",
        });

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents[1].FullContent.Should().Be("Old assessment");

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents[2].FullContent.Should().Be("Old advice");

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents[3].FullContent.Should().Be("New threat");

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents[4].FullContent.Should().Be("New advice");

        sut.EmitNextQueued().Should().BeFalse();
        _capturedEvents.Should().HaveCount(5);
    }

    [Fact]
    public void EmptyAnalysis_IsNoOp()
    {
        var sut = CreateSut();

        sut.OnStructuredAnalysis(new BrainAnalysisResult
        {
            VisualDescription = "  ",
            PositionAssessment = "",
            Threats = null,
            SuggestedAction = null,
        });

        sut.EmitNextQueued().Should().BeFalse();
        _capturedEvents.Should().BeEmpty();
    }

    [Fact]
    public void OnStructuredAnalysis_FiresAnalysisBatchQueued_InPriorityOrder()
    {
        var sut = CreateSut();
        IReadOnlyList<TimelineEvent>? batch = null;

        sut.AnalysisBatchQueued += events => batch = events;

        sut.OnStructuredAnalysis(new BrainAnalysisResult
        {
            Threats = "Back rank mate",
            PositionAssessment = "Equal but sharp",
            SuggestedAction = "Create luft",
        });

        batch.Should().NotBeNull();
        batch!.Select(evt => evt.Type).Should().ContainInOrder(
            EventOutputType.Danger,
            EventOutputType.Assessment,
            EventOutputType.SageAdvice);
    }

    [Fact]
    public void SingleEventBatch_WorksCorrectly()
    {
        var sut = CreateSut();

        sut.OnStructuredAnalysis(new BrainAnalysisResult
        {
            Threats = "Fork on e4",
        });

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents.Should().HaveCount(1);
        _capturedEvents[0].Type.Should().Be(EventOutputType.Danger);

        sut.EmitNextQueued().Should().BeFalse();
    }

    [Fact]
    public void EmitNextQueued_FiresAnalysisEventEmitted()
    {
        var sut = CreateSut();
        TimelineEvent? received = null;
        sut.AnalysisEventEmitted += e => received = e;

        sut.OnStructuredAnalysis(new BrainAnalysisResult { Threats = "Fork" });
        sut.EmitNextQueued();

        received.Should().NotBeNull();
        received!.Type.Should().Be(EventOutputType.Danger);
    }

    [Fact]
    public void RapidTripleBatch_PreservesArrivalOrder()
    {
        var sut = CreateSut();

        sut.OnStructuredAnalysis(new BrainAnalysisResult { Threats = "Threat 1" });
        sut.OnStructuredAnalysis(new BrainAnalysisResult { Threats = "Threat 2" });
        sut.OnStructuredAnalysis(new BrainAnalysisResult { Threats = "Threat 3" });

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents.Should().HaveCount(1);
        _capturedEvents[0].FullContent.Should().Be("Threat 1");

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents[1].FullContent.Should().Be("Threat 2");

        sut.EmitNextQueued().Should().BeTrue();
        _capturedEvents[2].FullContent.Should().Be("Threat 3");

        sut.EmitNextQueued().Should().BeFalse();
    }
}
