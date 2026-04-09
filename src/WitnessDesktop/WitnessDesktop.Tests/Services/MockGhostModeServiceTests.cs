using FluentAssertions;
using WitnessDesktop.Services;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class MockGhostModeServiceTests
{
    [Fact]
    public void ShowVideoCard_DoesNotThrow()
    {
        var sut = new MockGhostModeService();

        var act = () => sut.ShowVideoCard("/tmp/test.mp4", 5.0, 10.0, "REPLAY");
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowVideoCard_NullTitle_DoesNotThrow()
    {
        var sut = new MockGhostModeService();

        var act = () => sut.ShowVideoCard("/tmp/test.mp4", 0, 30.0, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void IGhostModeService_HasShowVideoCardMethod()
    {
        // Verify the interface contract includes ShowVideoCard
        var method = typeof(IGhostModeService).GetMethod("ShowVideoCard");
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(4);
        method.GetParameters()[0].ParameterType.Should().Be(typeof(string));
        method.GetParameters()[1].ParameterType.Should().Be(typeof(double));
        method.GetParameters()[2].ParameterType.Should().Be(typeof(double));
        method.GetParameters()[3].ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public void MockGhostModeService_ImplementsShowVideoCard()
    {
        // MockGhostModeService must implement IGhostModeService including ShowVideoCard
        var sut = new MockGhostModeService();
        IGhostModeService service = sut;

        var act = () => service.ShowVideoCard("/path/to/video.mp4", 2.5, 15.0, "TEST");
        act.Should().NotThrow();
    }
}
