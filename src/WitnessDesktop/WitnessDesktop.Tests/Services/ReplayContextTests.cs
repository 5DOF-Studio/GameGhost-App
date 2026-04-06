using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Services;

public class ReplayContextTests
{
    [Fact]
    public void ReplayContext_DefaultItems_IsEmptyList()
    {
        var ctx = new ReplayContext
        {
            SessionId = "sess-001",
            WindowStartUtc = DateTime.UtcNow.AddMinutes(-5),
            WindowEndUtc = DateTime.UtcNow
        };

        ctx.Items.Should().NotBeNull();
        ctx.Items.Should().BeEmpty();
    }

    [Fact]
    public void ReplayItem_ChatMessage_Kind()
    {
        var item = new ReplayItem
        {
            TimestampUtc = DateTime.UtcNow,
            Kind = ReplayItemKind.ChatMessage,
            MessageContent = "Hello",
            MessageRole = "User"
        };

        item.Kind.Should().Be(ReplayItemKind.ChatMessage);
        item.MessageContent.Should().Be("Hello");
        item.MessageRole.Should().Be("User");
        item.EventSummary.Should().BeNull();
        item.ArtifactPath.Should().BeNull();
    }

    [Fact]
    public void ReplayItem_TimelineEvent_Kind()
    {
        var item = new ReplayItem
        {
            TimestampUtc = DateTime.UtcNow,
            Kind = ReplayItemKind.TimelineEvent,
            EventSummary = "Position is solid",
            EventType = "Assessment",
            BrainSignal = "Stable",
            SuggestedAction = "Develop knight"
        };

        item.Kind.Should().Be(ReplayItemKind.TimelineEvent);
        item.EventSummary.Should().Be("Position is solid");
        item.EventType.Should().Be("Assessment");
        item.BrainSignal.Should().Be("Stable");
        item.SuggestedAction.Should().Be("Develop knight");
    }

    [Fact]
    public void ReplayItem_CaptureArtifact_Kind()
    {
        var item = new ReplayItem
        {
            TimestampUtc = DateTime.UtcNow,
            Kind = ReplayItemKind.CaptureArtifact,
            ArtifactPath = "/tmp/test.jpg",
            ArtifactExists = false
        };

        item.Kind.Should().Be(ReplayItemKind.CaptureArtifact);
        item.ArtifactPath.Should().Be("/tmp/test.jpg");
        item.ArtifactExists.Should().BeFalse();
    }

    [Fact]
    public void ReplayItemKind_HasThreeValues()
    {
        var values = Enum.GetValues<ReplayItemKind>();
        values.Should().HaveCount(3);
        values.Should().Contain(ReplayItemKind.ChatMessage);
        values.Should().Contain(ReplayItemKind.TimelineEvent);
        values.Should().Contain(ReplayItemKind.CaptureArtifact);
    }
}
