using FluentAssertions;
using Moq;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Replay;
using WitnessDesktop.ViewModels;
using Xunit;

namespace WitnessDesktop.Tests.ViewModels;

public class MainViewModelRecordingTests : MainViewModelTestBase
{
    public MainViewModelRecordingTests()
    {
        MockReplayRecording = new Mock<IReplayRecordingService>();
        MockSessionTrace = new Mock<ISessionTraceService>();
        MockSessionTrace.Setup(t => t.SessionId).Returns("test-session-123");
    }

    [Fact]
    public void Constructor_WithNullReplayRecording_DoesNotThrow()
    {
        MockReplayRecording = null;
        var sut = CreateSut();
        sut.Should().NotBeNull();
    }

    [Fact]
    public async Task StopSessionAsync_WhenRecording_StopsReplayRecording()
    {
        MockReplayRecording!.Setup(r => r.IsRecording).Returns(true);
        var sut = CreateSut();

        var method = typeof(MainViewModel).GetMethod("StopSessionAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(sut, null)!;

        MockReplayRecording.Verify(r => r.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task StopSessionAsync_WhenNotRecording_SkipsReplayStop()
    {
        MockReplayRecording!.Setup(r => r.IsRecording).Returns(false);
        var sut = CreateSut();

        var method = typeof(MainViewModel).GetMethod("StopSessionAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(sut, null)!;

        MockReplayRecording.Verify(r => r.StopAsync(), Times.Never);
    }
}
