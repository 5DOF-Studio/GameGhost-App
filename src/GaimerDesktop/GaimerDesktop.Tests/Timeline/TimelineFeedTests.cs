using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Timeline;

public class TimelineFeedTests
{
    private readonly Mock<ISessionManager> _mockSession;

    public TimelineFeedTests()
    {
        _mockSession = new Mock<ISessionManager>();
        _mockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);
    }

    private TimelineFeed CreateSut() => new(_mockSession.Object);

    [Fact]
    public void NewCapture_CreatesInGameCheckpoint()
    {
        var sut = CreateSut();

        var checkpoint = sut.NewCapture("screenshot-001.png", TimeSpan.FromMinutes(3), "auto");

        checkpoint.Context.Should().Be(SessionState.InGame);
        checkpoint.ScreenshotRef.Should().Be("screenshot-001.png");
        checkpoint.GameTimeIn.Should().Be(TimeSpan.FromMinutes(3));
        checkpoint.CaptureMethod.Should().Be("auto");
    }

    [Fact]
    public void NewCapture_PrependsCheckpoint()
    {
        var sut = CreateSut();
        var first = sut.NewCapture("first.png", TimeSpan.Zero, "auto");
        var second = sut.NewCapture("second.png", TimeSpan.FromSeconds(30), "auto");

        sut.Checkpoints[0].Should().BeSameAs(second);
        sut.Checkpoints[1].Should().BeSameAs(first);
    }

    [Fact]
    public void NewConversationCheckpoint_CreatesOutGameCheckpoint()
    {
        var sut = CreateSut();

        var checkpoint = sut.NewConversationCheckpoint();

        checkpoint.Context.Should().Be(SessionState.OutGame);
    }

    [Fact]
    public void AddEvent_CreatesEventLineInCurrentCheckpoint()
    {
        var sut = CreateSut();
        sut.NewCapture("ref.png", TimeSpan.Zero, "auto");

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Good move" });

        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(1);
        sut.CurrentCheckpoint.EventLines[0].Events.Should().HaveCount(1);
        sut.CurrentCheckpoint.EventLines[0].Events[0].Summary.Should().Be("Good move");
    }

    [Fact]
    public void AddEvent_GroupsSameOutputType()
    {
        var sut = CreateSut();
        sut.NewCapture("ref.png", TimeSpan.Zero, "auto");

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "First danger" });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second danger" });

        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(1);
        sut.CurrentCheckpoint.EventLines[0].Events.Should().HaveCount(2);
    }

    [Fact]
    public void AddEvent_DifferentType_CreatesNewEventLine()
    {
        var sut = CreateSut();
        sut.NewCapture("ref.png", TimeSpan.Zero, "auto");

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Danger" });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Opportunity, Summary = "Opportunity" });

        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(2);
    }

    [Fact]
    public void EnsureCurrentCheckpoint_AutoCreatesBasedOnState()
    {
        _mockSession.Setup(s => s.CurrentState).Returns(SessionState.OutGame);
        var sut = CreateSut();

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.GeneralChat, Summary = "Hello" });

        sut.Checkpoints.Should().HaveCount(1);
        sut.CurrentCheckpoint!.Context.Should().Be(SessionState.OutGame);
    }

    [Fact]
    public void CheckpointCreated_EventFires()
    {
        var sut = CreateSut();
        TimelineCheckpoint? received = null;
        sut.CheckpointCreated += (_, cp) => received = cp;

        var checkpoint = sut.NewCapture("ref.png", TimeSpan.Zero, "auto");

        received.Should().BeSameAs(checkpoint);
    }
}

public class TimelineCheckpointTests
{
    [Fact]
    public void ContextBadge_JustNow_WhenUnder5Seconds()
    {
        var cp = new TimelineCheckpoint { Timestamp = DateTime.UtcNow };
        cp.ContextBadge.Should().Be("just now");
    }

    [Fact]
    public void ContextBadge_SecondsAgo_WhenUnder60Seconds()
    {
        var cp = new TimelineCheckpoint { Timestamp = DateTime.UtcNow.AddSeconds(-30) };
        cp.ContextBadge.Should().Be("30s ago");
    }

    [Fact]
    public void ContextBadge_MinutesAgo_WhenUnder60Minutes()
    {
        var cp = new TimelineCheckpoint { Timestamp = DateTime.UtcNow.AddMinutes(-5) };
        cp.ContextBadge.Should().Be("5m ago");
    }

    [Fact]
    public void ContextBadge_HoursAgo_WhenUnder24Hours()
    {
        var cp = new TimelineCheckpoint { Timestamp = DateTime.UtcNow.AddHours(-3) };
        cp.ContextBadge.Should().Be("3h ago");
    }

    [Fact]
    public void ContextBadge_DaysAgo_WhenOver24Hours()
    {
        var cp = new TimelineCheckpoint { Timestamp = DateTime.UtcNow.AddDays(-2) };
        cp.ContextBadge.Should().Be("2d ago");
    }
}
