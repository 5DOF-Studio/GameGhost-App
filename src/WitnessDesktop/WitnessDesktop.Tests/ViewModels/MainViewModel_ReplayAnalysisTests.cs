using FluentAssertions;
using Moq;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.ViewModels;

public class MainViewModel_ReplayAnalysisTests
{
    [Fact]
    public void SegmentCompleted_EnqueuesOnOrchestrator()
    {
        var mockOrchestrator = new Mock<IReplayAnalysisOrchestrator>();
        var segment = new ReplaySegment
        {
            FilePath = "/tmp/seg.mp4",
            SessionId = "sess",
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            EndUtc = DateTimeOffset.UtcNow,
            SegmentIndex = 0
        };

        ReplaySegment? enqueued = null;
        mockOrchestrator.Setup(o => o.EnqueueSegment(It.IsAny<ReplaySegment>()))
            .Callback<ReplaySegment>(s => enqueued = s);

        var handler = new EventHandler<ReplaySegmentCompletedEventArgs>((_, e) =>
        {
            mockOrchestrator.Object.EnqueueSegment(e.Segment);
        });

        handler.Invoke(null, new ReplaySegmentCompletedEventArgs { Segment = segment });
        enqueued.Should().NotBeNull();
        enqueued!.FilePath.Should().Be("/tmp/seg.mp4");
    }

    [Fact]
    public void OrchestratorStartsWithSession()
    {
        var mockOrchestrator = new Mock<IReplayAnalysisOrchestrator>();
        mockOrchestrator.Object.Start(CancellationToken.None);
        mockOrchestrator.Verify(o => o.Start(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OrchestratorStopsWithDisconnect()
    {
        var mockOrchestrator = new Mock<IReplayAnalysisOrchestrator>();
        mockOrchestrator.Object.Stop();
        mockOrchestrator.Verify(o => o.Stop(), Times.Once);
    }
}
