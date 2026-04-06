using FluentAssertions;
using Moq;
using System.Threading;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.ViewModels;
using Xunit;

namespace WitnessDesktop.Tests.ViewModels;

/// <summary>
/// Tests for TopStripUpdated event wiring between BrainEventRouter and MainViewModel.
/// </summary>
public class MainViewModel_TopStripUpdated_Tests : MainViewModelTestBase
{
    [Fact]
    public void MainViewModel_TopStripUpdated_UpdatesAiDisplayContent()
    {
        // Arrange
        var sut = CreateSut();

        // Act — raise TopStripUpdated on the mock router
        MockRouter.Raise(r => r.TopStripUpdated += null, "Brain sees Sicilian Defense");

        // Assert
        sut.AiDisplayContent.Should().NotBeNull();
        sut.AiDisplayContent!.Text.Should().Be("Brain sees Sicilian Defense");
    }

    [Fact]
    public void MainViewModel_TopStripUpdated_SetsHasAiContentTrue()
    {
        // Arrange
        var sut = CreateSut();
        sut.HasAiContent.Should().BeFalse("no content before event fires");

        // Act
        MockRouter.Raise(r => r.TopStripUpdated += null, "Opening detected: King's Indian");

        // Assert
        sut.HasAiContent.Should().BeTrue();
        sut.HasNoAiContent.Should().BeFalse();
    }

    [Fact]
    public void MainViewModel_TopStripUpdated_MultipleUpdates_KeepsLatest()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        MockRouter.Raise(r => r.TopStripUpdated += null, "First analysis");
        MockRouter.Raise(r => r.TopStripUpdated += null, "Second analysis");

        // Assert — latest wins
        sut.AiDisplayContent!.Text.Should().Be("Second analysis");
    }

    [Fact]
    public void MainViewModel_TerminalBrainErrorConnected_PausesBrainOnly()
    {
        MockConversation.Setup(c => c.State).Returns(ConnectionState.Connected);
        MockConversation.Setup(c => c.IsConnected).Returns(true);
        var sut = CreateSut();
        sut.ConnectionState = ConnectionState.Connected;
        sut.SelectedTarget = CreateTestTarget();

        MockRouter.Raise(r => r.TerminalBrainErrorReceived += null, new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = "Brain service is temporarily unavailable after 3 attempts. Brain analysis paused to stop repeated failures.",
            ErrorFingerprint = "openrouter:http_500",
            AttemptCount = 3,
            RequestDisconnect = true
        });

        SpinWait.SpinUntil(() => MockBrain.Invocations.Any(i => i.Method.Name == nameof(MockBrain.Object.CancelAll)), 500);

        // Brain paused — NOT session disconnected
        MockBrain.Verify(b => b.CancelAll(), Times.Once);
        MockConversation.Verify(c => c.DisconnectAsync(), Times.Never);
        MockCapture.Verify(c => c.StopCaptureAsync(), Times.Never);
        sut.ChatMessages.Should().ContainSingle(m =>
            m.Role == MessageRole.System &&
            m.Content.Contains("temporarily unavailable"));
    }
}

/// <summary>
/// Tests that BrainEventRouter fires TopStripUpdated event from its routing methods.
/// Uses a real BrainEventRouter instance (not mocked).
/// </summary>
public class BrainEventRouter_TopStripUpdated_Tests
{
    private readonly Mock<ITimelineFeed> _mockTimeline;
    private string? _capturedTopStrip;
    private string? _capturedEvent;

    public BrainEventRouter_TopStripUpdated_Tests()
    {
        _mockTimeline = new Mock<ITimelineFeed>();
    }

    private BrainEventRouter CreateSut()
    {
        var router = new BrainEventRouter(
            _mockTimeline.Object,
            topStrip: s => _capturedTopStrip = s);
        router.TopStripUpdated += text => _capturedEvent = text;
        return router;
    }

    [Fact]
    public void OnImageAnalysis_FiresTopStripUpdated()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.OnImageAnalysis("test analysis text");

        // Assert
        _capturedEvent.Should().Be("test analysis text");
        _capturedTopStrip.Should().Be("test analysis text", "existing _topStrip callback still fires");
    }

    [Fact]
    public void OnScreenCapture_FiresTopStripUpdated()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.OnScreenCapture("screen001", TimeSpan.FromSeconds(42), "auto");

        // Assert
        _capturedEvent.Should().Contain("Analyzing capture");
    }

    [Fact]
    public void OnBrainHint_FiresTopStripUpdated()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.OnBrainHint(new BrainHint
        {
            Signal = "sage",
            Urgency = "low",
            Summary = "Consider castling",
            Evaluation = 30
        });

        // Assert
        _capturedEvent.Should().Be("Consider castling");
    }

    [Fact]
    public void OnProactiveAlert_FiresTopStripUpdated()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.OnProactiveAlert(
            new BrainHint { Signal = "danger", Urgency = "high", Summary = "Blunder!", Evaluation = -200 },
            "You're about to lose your queen!");

        // Assert
        _capturedEvent.Should().Be("You're about to lose your queen!");
    }

    [Fact]
    public void OnGeneralChat_FiresTopStripUpdated()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.OnGeneralChat("General chat message");

        // Assert
        _capturedEvent.Should().Be("General chat message");
    }
}
