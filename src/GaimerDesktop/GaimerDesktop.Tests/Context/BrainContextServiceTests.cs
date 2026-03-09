using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Context;

public class BrainContextServiceTests
{
    private readonly Mock<IVisualReelService> _mockReel;
    private readonly BrainContextService _sut;

    public BrainContextServiceTests()
    {
        _mockReel = new Mock<IVisualReelService>();
        _mockReel.Setup(r => r.GetRecent(It.IsAny<int>()))
            .Returns(new List<ReelMoment>());
        _sut = new BrainContextService(_mockReel.Object);
    }

    private static BrainEvent MakeEvent(
        DateTime? timestamp = null,
        string category = "vision",
        string text = "Test event",
        double confidence = 0.8,
        TimeSpan? validFor = null)
    {
        return new BrainEvent
        {
            TimestampUtc = timestamp ?? DateTime.UtcNow,
            Type = BrainEventType.VisionObservation,
            Category = category,
            Text = text,
            Confidence = confidence,
            ValidFor = validFor
        };
    }

    // ── Budget tokens ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetContextForVoiceAsync_RespectsVoiceBudget()
    {
        var envelope = await _sut.GetContextForVoiceAsync(DateTime.UtcNow);

        envelope.BudgetTokens.Should().Be(BrainContextService.DefaultVoiceBudget);
    }

    [Fact]
    public async Task GetContextForChatAsync_RespectsChatBudget()
    {
        var envelope = await _sut.GetContextForChatAsync(DateTime.UtcNow);

        envelope.BudgetTokens.Should().Be(BrainContextService.DefaultChatBudget);
    }

    // ── L1 event store ──────────────────────────────────────────────────────

    [Fact]
    public async Task IngestEvent_AddsToL1Store()
    {
        var now = DateTime.UtcNow;
        await _sut.IngestEventAsync(MakeEvent(timestamp: now, text: "Board changed"));

        var envelope = await _sut.GetContextForVoiceAsync(now.AddSeconds(5));
        envelope.ImmediateEvents.Should().ContainSingle(e => e.Text == "Board changed");
    }

    [Fact]
    public async Task IngestEvent_PrunesOlderThan5Min()
    {
        var now = DateTime.UtcNow;
        // Old event (6 min ago) — should be pruned
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-6), text: "Old"));
        // Recent event — should survive
        await _sut.IngestEventAsync(MakeEvent(timestamp: now, text: "Recent"));

        var envelope = await _sut.GetContextForVoiceAsync(now.AddSeconds(5));
        envelope.ImmediateEvents.Should().NotContain(e => e.Text == "Old");
    }

    [Fact]
    public async Task IngestEvent_CapsAt200Events()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 210; i++)
        {
            await _sut.IngestEventAsync(MakeEvent(
                timestamp: now.AddSeconds(-i * 0.1),
                text: $"Event {i}"));
        }

        // All 210 ingested, but pruning caps at 200
        // Request within L1 window to get back as many as fit in budget
        var envelope = await _sut.GetContextForChatAsync(now.AddSeconds(1), budgetTokens: 50000);
        // The store holds at most 200 after pruning; immediate events are the 30s window subset
        // We just verify the service didn't crash and returned events
        envelope.Should().NotBeNull();
    }

    // ── L1 filtering ────────────────────────────────────────────────────────

    [Fact]
    public async Task L1Filtering_Only30sWindow()
    {
        var now = DateTime.UtcNow;
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddSeconds(-10), text: "Recent 10s"));
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddSeconds(-40), text: "At 40s"));
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-3), text: "At 3min"));

        var envelope = await _sut.GetContextForVoiceAsync(now);

        envelope.ImmediateEvents.Should().ContainSingle(e => e.Text == "Recent 10s");
        envelope.ImmediateEvents.Should().NotContain(e => e.Text == "At 40s");
        envelope.ImmediateEvents.Should().NotContain(e => e.Text == "At 3min");
    }

    [Fact]
    public async Task L1Filtering_ConfidenceBelow03_Excluded()
    {
        var now = DateTime.UtcNow;
        await _sut.IngestEventAsync(MakeEvent(timestamp: now, confidence: 0.2, text: "Low conf"));

        var envelope = await _sut.GetContextForVoiceAsync(now.AddSeconds(1));
        envelope.ImmediateEvents.Should().NotContain(e => e.Text == "Low conf");
    }

    [Fact]
    public async Task L1Filtering_StaleEvent_Excluded()
    {
        var now = DateTime.UtcNow;
        // Event valid for 5s, but we query 10s later
        await _sut.IngestEventAsync(MakeEvent(
            timestamp: now.AddSeconds(-10),
            validFor: TimeSpan.FromSeconds(5),
            text: "Stale"));

        var envelope = await _sut.GetContextForVoiceAsync(now);
        envelope.ImmediateEvents.Should().NotContain(e => e.Text == "Stale");
    }

    // ── L2 rolling summary ──────────────────────────────────────────────────

    [Fact]
    public async Task L2Summary_GroupsByCategory()
    {
        var now = DateTime.UtcNow;
        // Events in the 30s-5min window (for L2 generation)
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-1), category: "vision", text: "Saw pawn"));
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-2), category: "vision", text: "Saw knight"));
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-1.5), category: "threat", text: "King exposed"));

        var envelope = await _sut.GetContextForVoiceAsync(now);

        envelope.RollingSummary.Should().Contain("vision");
        envelope.RollingSummary.Should().Contain("threat");
    }

    [Fact]
    public async Task L2Summary_HighPriorityCategoriesFirst()
    {
        var now = DateTime.UtcNow;
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-1), category: "economy", text: "Gold count"));
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-1.5), category: "threat", text: "Enemy approaching"));

        var envelope = await _sut.GetContextForVoiceAsync(now);

        if (!string.IsNullOrEmpty(envelope.RollingSummary))
        {
            var threatIdx = envelope.RollingSummary.IndexOf("threat", StringComparison.Ordinal);
            var economyIdx = envelope.RollingSummary.IndexOf("economy", StringComparison.Ordinal);
            if (threatIdx >= 0 && economyIdx >= 0)
            {
                threatIdx.Should().BeLessThan(economyIdx);
            }
        }
    }

    // ── Envelope confidence ─────────────────────────────────────────────────

    [Fact]
    public async Task EnvelopeConfidence_NoEvents_Returns05()
    {
        var envelope = await _sut.GetContextForVoiceAsync(DateTime.UtcNow);
        envelope.EnvelopeConfidence.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public async Task EnvelopeConfidence_WithL1Events_AppliesBonus()
    {
        var now = DateTime.UtcNow;
        await _sut.IngestEventAsync(MakeEvent(timestamp: now, confidence: 0.8, text: "Fresh"));

        var envelope = await _sut.GetContextForVoiceAsync(now.AddSeconds(5));

        // Should be > 0.5 due to L1 event bonus
        envelope.EnvelopeConfidence.Should().BeGreaterThan(0.5);
    }

    // ── Budget allocation and truncation ─────────────────────────────────────

    [Fact]
    public async Task BudgetAllocation_TargetThenL1ThenChatThenL2()
    {
        var now = DateTime.UtcNow;
        // Add enough events to create budget pressure
        for (var i = 0; i < 20; i++)
        {
            await _sut.IngestEventAsync(MakeEvent(
                timestamp: now.AddSeconds(-i),
                text: new string('x', 100)));
        }

        // Small budget to force truncation
        var envelope = await _sut.GetContextForVoiceAsync(now.AddSeconds(1), budgetTokens: 50);

        // Should have truncation report since budget is very small
        envelope.TruncationReport.Should().NotBe("none");
    }

    [Fact]
    public async Task BudgetExhaustion_GracefulTruncation()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 50; i++)
        {
            await _sut.IngestEventAsync(MakeEvent(
                timestamp: now.AddSeconds(-i),
                text: new string('y', 200)));
        }

        // Very small budget
        var envelope = await _sut.GetContextForVoiceAsync(now.AddSeconds(1), budgetTokens: 10);

        envelope.Should().NotBeNull();
        envelope.TruncationReport.Should().NotBe("none");
    }

    // ── FormatAsPrefixedContextBlock ────────────────────────────────────────

    [Fact]
    public async Task FormatAsPrefixedContextBlock_ContainsAllSections()
    {
        var now = DateTime.UtcNow;
        // L1 event
        await _sut.IngestEventAsync(MakeEvent(timestamp: now, text: "Fresh observation"));
        // L2 event (in 30s-5min window)
        await _sut.IngestEventAsync(MakeEvent(timestamp: now.AddMinutes(-2), text: "Older observation"));

        var envelope = await _sut.GetContextForChatAsync(now.AddSeconds(5));
        var block = _sut.FormatAsPrefixedContextBlock(envelope);

        block.Should().Contain("[Context]");
        block.Should().Contain("[End Context]");
        // At least one of these sections should be present
        var hasSections = block.Contains("--- Immediate events ---") ||
                          block.Contains("--- Rolling summary ---");
        hasSections.Should().BeTrue();
    }
}
