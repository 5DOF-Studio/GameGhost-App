using SkiaSharp;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Tests.Helpers;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for infrastructure methods added in Plan 14-06 Task 1:
/// - IFrameDiffService.ResetHash()
/// - IBrainContextService.FlushEvents()
/// </summary>
public class InfrastructureMethodsTests
{
    // ── ResetHash on FrameDiffService ─────────────────────────────────────

    [Fact]
    public void ResetHash_ResetsInternalHashState()
    {
        // Verify ResetHash sets internal hash to 0 by checking ComputeHash + CompareHashes
        // indirectly through HasChanged behavior.
        // A checkerboard image has a non-zero dHash. After feeding it in, calling ResetHash,
        // then feeding the SAME image again should detect change (compare against 0 hash).
        var sut = new FrameDiffService(defaultThreshold: 1);
        var checkerboard = TestImageFactory.CreateCheckerboardPng(100, 100, 10, SKColors.Black, SKColors.White);

        // Verify checkerboard has non-zero hash
        var hash = sut.ComputeHash(checkerboard);
        hash.Should().NotBe(0UL, "checkerboard should produce non-zero dHash");

        // First HasChanged: compares checkerboard hash vs 0 (initial). Should change.
        sut.HasChanged(checkerboard, threshold: 1).Should().BeTrue("first image vs initial 0 hash");

        // Same image again: hash vs hash = distance 0. Should NOT change.
        sut.HasChanged(checkerboard, threshold: 1).Should().BeFalse("same image should not change");

        // Reset hash back to 0
        sut.ResetHash();

        // Same image again: hash vs 0 (reset) = should detect change again
        sut.HasChanged(checkerboard, threshold: 1).Should().BeTrue("after ResetHash, same image triggers change");
    }

    [Fact]
    public void ResetHash_ClearsLastChangeTime()
    {
        // Use a 1-hour debounce so the second call is guaranteed to be blocked
        var sut = new FrameDiffService(debounceWindow: TimeSpan.FromHours(1), defaultThreshold: 1);
        var checkerboard = TestImageFactory.CreateCheckerboardPng(100, 100, 10, SKColors.Black, SKColors.White);

        // First call triggers (no debounce issue since _lastChangeTime = MinValue)
        sut.HasChanged(checkerboard, threshold: 1).Should().BeTrue("first call always triggers");

        // Reset and verify debounce is also cleared
        sut.ResetHash();

        // After reset, debounce timer is cleared AND hash is 0, so next change goes through
        var afterReset = sut.HasChanged(checkerboard, threshold: 1);
        afterReset.Should().BeTrue("ResetHash should clear debounce timer allowing immediate detection");
    }

    [Fact]
    public void ResetHash_ImplementsInterfaceMethod()
    {
        IFrameDiffService sut = new FrameDiffService();

        // Should compile and not throw
        var act = () => sut.ResetHash();
        act.Should().NotThrow();
    }

    // ── FlushEvents on BrainContextService ────────────────────────────────

    [Fact]
    public async Task FlushEvents_ClearsAllL1Events()
    {
        var mockVisualReel = new Mock<IVisualReelService>();
        mockVisualReel.Setup(v => v.GetRecent(It.IsAny<int>()))
            .Returns(Array.Empty<ReelMoment>());
        var sut = new BrainContextService(mockVisualReel.Object);

        // Ingest some events
        await sut.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Type = BrainEventType.VisionObservation,
            Category = "vision",
            Text = "Board position analyzed",
            Confidence = 0.9
        });
        await sut.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Type = BrainEventType.GameplayState,
            Category = "tool",
            Text = "Stockfish analysis result",
            Confidence = 0.8
        });

        // Verify events exist before flush
        var envelopeBefore = await sut.GetContextForVoiceAsync(DateTime.UtcNow);
        envelopeBefore.ImmediateEvents.Should().NotBeEmpty("events were ingested");

        // Flush
        sut.FlushEvents();

        // Verify events are gone
        var envelopeAfter = await sut.GetContextForVoiceAsync(DateTime.UtcNow);
        envelopeAfter.ImmediateEvents.Should().BeEmpty("FlushEvents should clear all L1 events");
    }

    [Fact]
    public void FlushEvents_ImplementsInterfaceMethod()
    {
        var mockVisualReel = new Mock<IVisualReelService>();
        mockVisualReel.Setup(v => v.GetRecent(It.IsAny<int>()))
            .Returns(Array.Empty<ReelMoment>());
        IBrainContextService sut = new BrainContextService(mockVisualReel.Object);

        // Should compile and not throw
        var act = () => sut.FlushEvents();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task FlushEvents_AllowsNewEventsAfterFlush()
    {
        var mockVisualReel = new Mock<IVisualReelService>();
        mockVisualReel.Setup(v => v.GetRecent(It.IsAny<int>()))
            .Returns(Array.Empty<ReelMoment>());
        var sut = new BrainContextService(mockVisualReel.Object);

        // Ingest, flush, then ingest again
        await sut.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Type = BrainEventType.VisionObservation,
            Category = "vision",
            Text = "Before flush",
            Confidence = 0.9
        });

        sut.FlushEvents();

        await sut.IngestEventAsync(new BrainEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Type = BrainEventType.VisionObservation,
            Category = "vision",
            Text = "After flush",
            Confidence = 0.9
        });

        var envelope = await sut.GetContextForVoiceAsync(DateTime.UtcNow);
        envelope.ImmediateEvents.Should().HaveCount(1);
        envelope.ImmediateEvents[0].Text.Should().Be("After flush");
    }

}
