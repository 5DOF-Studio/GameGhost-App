using Microsoft.Extensions.Logging;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Brain;

namespace WitnessDesktop.Tests.Brain;

/// <summary>
/// Tests for MockBrainService TrySubmitFrame implementation and counter-bug fixes.
/// Mirrors the same patterns tested in FrameSlotTests (production) and
/// OpenRouterBrainServiceTests (counter regression).
/// </summary>
public class MockBrainServiceTests : IDisposable
{
    private readonly MockBrainService _sut;

    public MockBrainServiceTests()
    {
        _sut = new MockBrainService(Mock.Of<ILogger<MockBrainService>>());
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── TrySubmitFrame ──────────────────────────────────────────────────────

    [Fact]
    public void TrySubmitFrame_ReturnsTrue_WhenNotDisposed()
    {
        var result = _sut.TrySubmitFrame(new byte[] { 0x89, 0x50 }, "test context");

        result.Should().BeTrue();
    }

    [Fact]
    public void TrySubmitFrame_ReturnsFalse_WhenDisposed()
    {
        _sut.Dispose();

        var result = _sut.TrySubmitFrame(new byte[] { 0x89, 0x50 }, "test context");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TrySubmitFrame_ProducesResult_OnChannel()
    {
        _sut.TrySubmitFrame(new byte[] { 0x89, 0x50 }, "test context");

        using var cts = new CancellationTokenSource(5000);
        var result = await _sut.Results.ReadAsync(cts.Token);

        result.Type.Should().Be(BrainResultType.ImageAnalysis);
        result.AnalysisText.Should().NotBeNullOrEmpty();
        result.CorrelationId.Should().NotBeNullOrEmpty();
    }

    // ── Consumer Loop Survives CancelAll ────────────────────────────────────

    [Fact]
    public async Task ConsumerLoop_SurvivesCancelAll_ProcessesFrameAfterCancel()
    {
        // Submit frame and verify processing works
        _sut.TrySubmitFrame(new byte[] { 1, 2, 3 }, "pre-cancel frame");
        using var cts1 = new CancellationTokenSource(5000);
        var result1 = await _sut.Results.ReadAsync(cts1.Token);
        result1.Type.Should().Be(BrainResultType.ImageAnalysis);

        // CancelAll — simulates session disconnect
        _sut.CancelAll();
        await Task.Delay(100);

        // Submit another frame AFTER CancelAll — consumer loop must still be alive
        _sut.TrySubmitFrame(new byte[] { 4, 5, 6 }, "post-cancel frame");
        using var cts2 = new CancellationTokenSource(5000);
        var result2 = await _sut.Results.ReadAsync(cts2.Token);
        result2.Type.Should().Be(BrainResultType.ImageAnalysis,
            "consumer loop should survive CancelAll and process new frames");
    }

    // ── Counter Bug Fix Tests ───────────────────────────────────────────────

    [Fact]
    public async Task SubmitImageAsync_CounterReturnsToZero_AfterPreCancelledToken()
    {
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        await _sut.SubmitImageAsync(new byte[] { 1 }, "ctx", preCancelled.Token);

        // Wait for Task.Run to resolve
        await Task.Delay(500);

        _sut.IsBusy.Should().BeFalse(
            "counter must return to 0 even when token is pre-cancelled — " +
            "same critical bug pattern as production service");
    }

    [Fact]
    public async Task SubmitQueryAsync_CounterReturnsToZero_AfterPreCancelledToken()
    {
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        var envelope = new SharedContextEnvelope { Intent = "general" };
        await _sut.SubmitQueryAsync("query", envelope, preCancelled.Token);

        // Wait for Task.Run to resolve
        await Task.Delay(500);

        _sut.IsBusy.Should().BeFalse(
            "counter must return to 0 even when token is pre-cancelled");
    }
}
