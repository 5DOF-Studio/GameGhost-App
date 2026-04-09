using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;

namespace WitnessDesktop.Tests.Brain;

/// <summary>
/// Tests that BrainEventRouter respects IVoiceDeliveryGate for voice delivery paths.
/// Gate only affects voice — timeline routing is unaffected.
/// Null gate (backward compat) defaults to Deliver.
/// </summary>
public class BrainEventRouterVoiceGateTests
{
    private readonly Mock<ITimelineFeed> _mockTimeline = new();
    private readonly Mock<IConversationProvider> _mockVoice = new();
    private readonly Mock<IVoiceDeliveryGate> _mockGate = new();

    public BrainEventRouterVoiceGateTests()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        _mockVoice.Setup(v => v.SendContextualUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockVoice.Setup(v => v.SendContextualUpdateWithResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private BrainEventRouter CreateSut(IVoiceDeliveryGate? gate = null)
    {
        return new BrainEventRouter(
            _mockTimeline.Object,
            _mockVoice.Object,
            voiceDeliveryGate: gate);
    }

    private static BrainHint MakeHint(
        string signal = "sage",
        string urgency = "low",
        string summary = "Test hint") => new()
    {
        Signal = signal,
        Urgency = urgency,
        Summary = summary,
        Evaluation = 0,
    };

    // ── OnBrainHint voice gating ────────────────────────────────────────────

    [Fact]
    public void OnBrainHint_GateDeliver_SendsToVoice()
    {
        _mockGate.Setup(g => g.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert))
            .Returns(DeliveryDecision.Deliver);
        var sut = CreateSut(_mockGate.Object);

        sut.OnBrainHint(MakeHint());

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnBrainHint_GateSuppress_SkipsVoice()
    {
        _mockGate.Setup(g => g.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert))
            .Returns(DeliveryDecision.Suppress);
        var sut = CreateSut(_mockGate.Object);

        sut.OnBrainHint(MakeHint());

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── OnProactiveAlert voice gating ───────────────────────────────────────

    [Fact]
    public void OnProactiveAlert_HighUrgency_GateDeliver_SendsToVoice()
    {
        _mockGate.Setup(g => g.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert))
            .Returns(DeliveryDecision.Deliver);
        var sut = CreateSut(_mockGate.Object);

        sut.OnProactiveAlert(MakeHint(urgency: "high"), "Critical!");

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnProactiveAlert_HighUrgency_GateSuppress_SkipsVoice()
    {
        _mockGate.Setup(g => g.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert))
            .Returns(DeliveryDecision.Suppress);
        var sut = CreateSut(_mockGate.Object);

        sut.OnProactiveAlert(MakeHint(urgency: "high"), "Critical!");

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Backward compatibility (null gate) ──────────────────────────────────

    [Fact]
    public void OnBrainHint_NullGate_DeliversToVoice()
    {
        var sut = CreateSut(gate: null);

        sut.OnBrainHint(MakeHint());

        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Timeline unaffected by gate ─────────────────────────────────────────

    [Fact]
    public void OnBrainHint_GateSuppress_StillRoutesToTimeline()
    {
        _mockGate.Setup(g => g.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert))
            .Returns(DeliveryDecision.Suppress);
        var sut = CreateSut(_mockGate.Object);

        sut.OnBrainHint(MakeHint(signal: "danger"));

        _mockTimeline.Verify(t => t.AddEvent(
            It.Is<TimelineEvent>(e => e.Type == EventOutputType.Danger)), Times.Once);
        // Voice should be suppressed
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
