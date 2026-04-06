using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Conversation;

namespace WitnessDesktop.Tests.Brain;

/// <summary>
/// Tests for BrainEventRouter wiring: deferred response routing,
/// QueueReminder → actual reminder enqueue, and proactive alert reminder queueing.
/// These cover the medium-risk integration gaps between the gate decision and the actual side effects.
/// Uses RouteBrainResultForTest (internal) instead of channel consumer for deterministic execution.
/// </summary>
public class BrainEventRouterWiringTests
{
    private readonly Mock<ITimelineFeed> _mockTimeline = new();
    private readonly Mock<IConversationProvider> _mockVoice = new();
    private readonly Mock<IVoiceDeliveryGate> _mockGate = new();
    private readonly Mock<IReminderQueue> _mockReminders = new();
    private readonly Mock<IExchangeManager> _mockExchange = new();

    public BrainEventRouterWiringTests()
    {
        _mockVoice.Setup(v => v.IsConnected).Returns(true);
        _mockVoice.Setup(v => v.SendContextualUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private BrainEventRouter CreateSut()
    {
        return new BrainEventRouter(
            _mockTimeline.Object,
            _mockVoice.Object,
            voiceDeliveryGate: _mockGate.Object,
            reminderQueue: _mockReminders.Object,
            exchangeManager: _mockExchange.Object);
    }

    // ── Deferred Response: Exchange Active → Voice Delivery ──────────

    [Fact]
    public void RouteBrainResult_DeferredAnswer_ExchangeActive_DeliversToVoice()
    {
        _mockExchange.Setup(e => e.IsExchangeActive).Returns(true);
        // Suppress the general voice forwarding path so we only test the deferred path
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.Suppress);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            IsDeferredAnswer = true,
            VoiceNarration = "The engine recommends Nf3",
            AnalysisText = "Engine analysis complete",
        };

        sut.RouteBrainResultForTest(result);

        // Deferred path delivers to voice directly (bypasses gate)
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.Is<string>(s => s.Contains("Nf3")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Reminder queue should NOT be called (exchange is active)
        _mockReminders.Verify(r => r.Supersede(
            It.IsAny<BargeInCategory>(), It.IsAny<ReminderItem>()), Times.Never);
    }

    // ── Deferred Response: Exchange Expired → Queue Reminder ─────────

    [Fact]
    public void RouteBrainResult_DeferredAnswer_ExchangeExpired_QueuesReminder()
    {
        _mockExchange.Setup(e => e.IsExchangeActive).Returns(false);
        // Suppress general voice path to isolate deferred path behavior
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.Suppress);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            IsDeferredAnswer = true,
            VoiceNarration = "The engine recommends Nf3",
            AnalysisText = "Engine analysis complete",
        };

        sut.RouteBrainResultForTest(result);

        _mockReminders.Verify(r => r.Supersede(
            BargeInCategory.ToolExecution,
            It.Is<ReminderItem>(i => i.Content.Contains("Nf3") && i.Category == BargeInCategory.ToolExecution)),
            Times.Once);

        // Voice should NOT be called (exchange expired + general path suppressed)
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Deferred Response: No VoiceNarration → No Delivery or Queue ──

    [Fact]
    public void RouteBrainResult_DeferredAnswer_NullNarration_NoDeferredAction()
    {
        _mockExchange.Setup(e => e.IsExchangeActive).Returns(true);
        // Suppress general voice path to isolate deferred path
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.Suppress);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            IsDeferredAnswer = true,
            VoiceNarration = null, // No narration — deferred path skips entirely
            AnalysisText = "Result without narration",
        };

        sut.RouteBrainResultForTest(result);

        // Neither voice delivery nor reminder queue from deferred path
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockReminders.Verify(r => r.Supersede(
            It.IsAny<BargeInCategory>(), It.IsAny<ReminderItem>()), Times.Never);
    }

    // ── QueueReminder in RouteBrainResult → Actual Enqueue ──────────

    [Fact]
    public void RouteBrainResult_GateReturnsQueueReminder_ProactiveAlert_EnqueuesAsCallOut()
    {
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.QueueReminder);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            VoiceNarration = "Watch out for the fork!",
            AnalysisText = "Fork detected",
            Hint = new BrainHint { Signal = "danger", Urgency = "high", Summary = "Fork" },
        };

        sut.RouteBrainResultForTest(result);

        // ProactiveAlert maps to CallOut category in the voice forwarding path
        _mockReminders.Verify(r => r.Supersede(
            BargeInCategory.CallOut,
            It.Is<ReminderItem>(i => i.Category == BargeInCategory.CallOut
                && i.Content.Contains("fork"))),
            Times.AtLeastOnce);
    }

    // ── QueueReminder: ImageAnalysis → FreeCommentary Category ───────

    [Fact]
    public void RouteBrainResult_GateQueueReminder_ImageAnalysis_MapsFreeCommentary()
    {
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.QueueReminder);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            VoiceNarration = "Position looks equal",
            AnalysisText = "Position is roughly balanced",
        };

        sut.RouteBrainResultForTest(result);

        _mockReminders.Verify(r => r.Supersede(
            BargeInCategory.FreeCommentary,
            It.Is<ReminderItem>(i => i.Category == BargeInCategory.FreeCommentary)),
            Times.AtLeastOnce);
    }

    // ── QueueReminder: ToolResult → ToolExecution Category ──────────

    [Fact]
    public void RouteBrainResult_GateQueueReminder_ToolResult_MapsToolExecution()
    {
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.QueueReminder);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            VoiceNarration = "Stockfish says Nf3 is best",
            AnalysisText = "Tool completed",
        };

        sut.RouteBrainResultForTest(result);

        _mockReminders.Verify(r => r.Supersede(
            BargeInCategory.ToolExecution,
            It.Is<ReminderItem>(i => i.Category == BargeInCategory.ToolExecution)),
            Times.AtLeastOnce);
    }

    // ── OnProactiveAlert QueueReminder → Enqueue as CallOut ─────────

    [Fact]
    public void OnProactiveAlert_GateNotDeliver_HighUrgency_EnqueuesAsCallOut()
    {
        // Gate returns QueueReminder — the if on line 295 fails (checks == Deliver)
        // The else-if on line 304 fires because urgency is high and _reminderQueue != null
        _mockGate.Setup(g => g.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert))
            .Returns(DeliveryDecision.QueueReminder);
        var sut = CreateSut();

        var hint = new BrainHint
        {
            Signal = "danger",
            Urgency = "high",
            Summary = "Mate in 2!",
            Evaluation = -500,
        };

        sut.OnProactiveAlert(hint, "Mate threat detected!");

        _mockReminders.Verify(r => r.Supersede(
            BargeInCategory.CallOut,
            It.Is<ReminderItem>(i => i.Category == BargeInCategory.CallOut)),
            Times.Once);

        // Voice should NOT be called (gate did not return Deliver)
        _mockVoice.Verify(v => v.SendContextualUpdateAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Gate Deliver → No Reminder Enqueue ──────────────────────────

    [Fact]
    public void RouteBrainResult_GateDeliver_DoesNotEnqueueReminder()
    {
        _mockGate.Setup(g => g.ShouldDeliver(It.IsAny<BrainResultPriority>(), It.IsAny<BrainResultType>()))
            .Returns(DeliveryDecision.Deliver);
        var sut = CreateSut();

        var result = new BrainResult
        {
            Type = BrainResultType.ProactiveAlert,
            VoiceNarration = "Delivered directly",
            AnalysisText = "Alert",
            Priority = BrainResultPriority.WhenIdle,
            Hint = new BrainHint { Signal = "danger", Urgency = "high", Summary = "Test" },
        };

        sut.RouteBrainResultForTest(result);

        // Deliver → voice gets it, reminder queue should NOT be called
        _mockReminders.Verify(r => r.Supersede(
            It.IsAny<BargeInCategory>(), It.IsAny<ReminderItem>()), Times.Never);
        _mockReminders.Verify(r => r.Enqueue(It.IsAny<ReminderItem>()), Times.Never);
    }
}
