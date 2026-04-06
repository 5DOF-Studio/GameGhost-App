using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Tests for timeline routing of user messages and brain chat replies (A4).
/// Verifies OnUserMessage adds DirectMessage events to timeline.
/// </summary>
public class ChatTimelineRoutingTests
{
    [Fact]
    public void OnUserMessage_AddsDirectMessageToTimeline_WithUserRole()
    {
        var timeline = new Mock<ITimelineFeed>();
        TimelineEvent? capturedEvent = null;
        timeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => capturedEvent = e);

        var router = new BrainEventRouter(timeline.Object);

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "What move should I play here?"
        };

        router.OnUserMessage(userMsg);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventOutputType.DirectMessage);
        capturedEvent.Role.Should().Be(MessageRole.User);
        capturedEvent.LinkedMessage.Should().Be(userMsg);
    }

    [Fact]
    public void OnUserMessage_TruncatesSummaryAt60Chars()
    {
        var timeline = new Mock<ITimelineFeed>();
        TimelineEvent? capturedEvent = null;
        timeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => capturedEvent = e);

        var router = new BrainEventRouter(timeline.Object);

        var longContent = new string('a', 100);
        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = longContent
        };

        router.OnUserMessage(userMsg);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Summary.Length.Should().BeLessThanOrEqualTo(60);
    }

    [Fact]
    public void OnUserMessage_SetsFullContent()
    {
        var timeline = new Mock<ITimelineFeed>();
        TimelineEvent? capturedEvent = null;
        timeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => capturedEvent = e);

        var router = new BrainEventRouter(timeline.Object);

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "What move should I play here?"
        };

        router.OnUserMessage(userMsg);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.FullContent.Should().Be("What move should I play here?");
    }

    [Fact]
    public void OnUserMessage_SetsCorrectIconAndColors()
    {
        var timeline = new Mock<ITimelineFeed>();
        TimelineEvent? capturedEvent = null;
        timeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => capturedEvent = e);

        var router = new BrainEventRouter(timeline.Object);
        var userMsg = new ChatMessage { Role = MessageRole.User, Content = "test" };

        router.OnUserMessage(userMsg);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Icon.Should().Be(EventIconMap.GetIcon(EventOutputType.DirectMessage));
        capturedEvent.CapsuleColorHex.Should().Be(EventIconMap.GetCapsuleColorHex(EventOutputType.DirectMessage));
        capturedEvent.CapsuleStrokeHex.Should().Be(EventIconMap.GetCapsuleStrokeHex(EventOutputType.DirectMessage));
    }

    // ── BrainChatReplyReceived event ─────────────────────────────────────

    [Fact]
    public void BrainChatReplyReceived_FiresOnToolResult()
    {
        var timeline = new Mock<ITimelineFeed>();
        string? receivedReply = null;

        var router = new BrainEventRouter(timeline.Object);
        router.BrainChatReplyReceived += reply => receivedReply = reply;

        // SubmitQueryAsync writes BrainResultType.ToolResult
        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "You should play Nf3 to develop your knight.",
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };
        router.RouteBrainResultForTest(result);

        receivedReply.Should().Be("You should play Nf3 to develop your knight.");
    }

    [Fact]
    public void RouteBrainResult_ToolCalls_AddsTimelineEventsWithToolMetadata()
    {
        var timeline = new Mock<ITimelineFeed>();
        var capturedEvents = new List<TimelineEvent>();
        timeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => capturedEvents.Add(e));

        var router = new BrainEventRouter(timeline.Object);
        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = "Final reply",
            ToolCalls =
            [
                new ToolCallInfo
                {
                    ToolName = "web_search",
                    OutputJson = "{\"status\":\"ok\"}",
                    DurationMs = 812,
                    Success = true
                }
            ],
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        capturedEvents.Should().HaveCount(2);
        capturedEvents[0].ToolCall.Should().NotBeNull();
        capturedEvents[0].Icon.Should().Be("tool_search.svg");
        capturedEvents[0].Summary.Should().Be("Searched Internet");
        capturedEvents[1].Summary.Should().Be("Final reply");
    }

    [Fact]
    public void RouteBrainResult_ImageAnalysisWithToolCalls_AddsToolEventsBeforeAnalysis()
    {
        var timeline = new Mock<ITimelineFeed>();
        var capturedEvents = new List<TimelineEvent>();
        timeline.Setup(t => t.AddEvent(It.IsAny<TimelineEvent>()))
            .Callback<TimelineEvent>(e => capturedEvents.Add(e));

        var router = new BrainEventRouter(timeline.Object);
        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Board looks stable",
            ToolCalls =
            [
                new ToolCallInfo
                {
                    ToolName = "game_journal",
                    OutputJson = "{\"status\":\"ok\"}",
                    Success = true
                }
            ],
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);

        // D-039: ImageAnalysis no longer goes to timeline; only tool call event remains
        capturedEvents.Should().HaveCount(1);
        capturedEvents[0].ToolCall.Should().NotBeNull();
        capturedEvents[0].Summary.Should().Be("Updating journal");
    }

    [Fact]
    public void BrainChatReplyReceived_DoesNotFireOnImageAnalysis()
    {
        var timeline = new Mock<ITimelineFeed>();
        string? receivedReply = null;

        var router = new BrainEventRouter(timeline.Object);
        router.BrainChatReplyReceived += reply => receivedReply = reply;

        var result = new BrainResult
        {
            Type = BrainResultType.ImageAnalysis,
            AnalysisText = "Board analysis text",
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };
        router.RouteBrainResultForTest(result);

        receivedReply.Should().BeNull("BrainChatReplyReceived only fires on ToolResult");
    }

    [Fact]
    public void BrainChatReplyReceived_DoesNotFireWhenAnalysisTextIsNull()
    {
        var timeline = new Mock<ITimelineFeed>();
        string? receivedReply = null;

        var router = new BrainEventRouter(timeline.Object);
        router.BrainChatReplyReceived += reply => receivedReply = reply;

        var result = new BrainResult
        {
            Type = BrainResultType.ToolResult,
            AnalysisText = null,
            Priority = BrainResultPriority.WhenIdle,
            CreatedAt = DateTimeOffset.UtcNow
        };
        router.RouteBrainResultForTest(result);

        receivedReply.Should().BeNull("no reply when analysis text is null");
    }

    [Fact]
    public void RouteBrainResult_RepeatedBrainError_DedupsTimelineAndTerminalEvent()
    {
        var timeline = new Mock<ITimelineFeed>();
        var router = new BrainEventRouter(timeline.Object);
        var terminalCount = 0;
        BrainResult? terminal = null;
        router.TerminalBrainErrorReceived += result =>
        {
            terminal = result;
            terminalCount++;
        };

        var result = new BrainResult
        {
            Type = BrainResultType.Error,
            AnalysisText = "Brain service is temporarily unavailable after 3 attempts. Session disconnected to stop repeated failures.",
            Priority = BrainResultPriority.Silent,
            ErrorFingerprint = "openrouter:http_500",
            AttemptCount = 3,
            RequestDisconnect = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        router.RouteBrainResultForTest(result);
        router.RouteBrainResultForTest(result);

        timeline.Verify(t => t.AddEvent(It.Is<TimelineEvent>(e =>
            e.Type == EventOutputType.SystemError &&
            e.FullContent == result.AnalysisText)), Times.Once);
        terminal.Should().NotBeNull();
        terminal!.ErrorFingerprint.Should().Be("openrouter:http_500");
        terminalCount.Should().Be(2);
    }

    // ── Interface declares OnUserMessage and BrainChatReplyReceived ──────

    [Fact]
    public void IBrainEventRouter_DeclaresOnUserMessage()
    {
        IBrainEventRouter router = new BrainEventRouter(new Mock<ITimelineFeed>().Object);

        var act = () => router.OnUserMessage(new ChatMessage { Role = MessageRole.User, Content = "test" });
        act.Should().NotThrow();
    }
}
