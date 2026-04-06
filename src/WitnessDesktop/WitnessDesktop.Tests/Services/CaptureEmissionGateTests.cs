using FluentAssertions;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class CaptureEmissionGateTests
{
    private static readonly byte[] FrameA = [1, 2, 3, 4];
    private static readonly byte[] FrameB = [4, 3, 2, 1];

    [Fact]
    public void ShouldEmit_FirstFrame_ReturnsTrue()
    {
        var sut = new CaptureEmissionGate();

        sut.ShouldEmit(FrameA).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_UnchangedFrame_ReturnsFalse()
    {
        var sut = new CaptureEmissionGate();
        sut.ShouldEmit(FrameA).Should().BeTrue();

        sut.ShouldEmit(FrameA).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmit_ChangedFrame_ReturnsTrue()
    {
        var sut = new CaptureEmissionGate();
        sut.ShouldEmit(FrameA).Should().BeTrue();

        sut.ShouldEmit(FrameB).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmit_RevertedFrame_ReturnsTrue()
    {
        var sut = new CaptureEmissionGate();
        sut.ShouldEmit(FrameA).Should().BeTrue();
        sut.ShouldEmit(FrameB).Should().BeTrue();

        // Frame reverts to A — hash differs from last emitted (B), so emit
        sut.ShouldEmit(FrameA).Should().BeTrue();
    }

    [Fact]
    public void Reset_AllowsReEmissionOfSameFrame()
    {
        var sut = new CaptureEmissionGate();
        sut.ShouldEmit(FrameA).Should().BeTrue();
        sut.ShouldEmit(FrameA).Should().BeFalse();

        sut.Reset();

        sut.ShouldEmit(FrameA).Should().BeTrue();
    }
}
