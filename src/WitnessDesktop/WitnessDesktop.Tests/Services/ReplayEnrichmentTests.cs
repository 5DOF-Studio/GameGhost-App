using WitnessDesktop.Services;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Services;

public class ReplayEnrichmentTests
{
    [Fact]
    public void AssembleReplayContext_FormatsChronologically()
    {
        // Arrange
        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);
        var context = new ReplayContext
        {
            SessionId = "sess-fmt",
            WindowStartUtc = baseTime,
            WindowEndUtc = baseTime.AddMinutes(1),
            Items = new List<ReplayItem>
            {
                new()
                {
                    TimestampUtc = baseTime.AddSeconds(1),
                    Kind = ReplayItemKind.ChatMessage,
                    MessageRole = "User",
                    MessageContent = "What is happening?"
                },
                new()
                {
                    TimestampUtc = baseTime.AddSeconds(2),
                    Kind = ReplayItemKind.TimelineEvent,
                    EventType = "Assessment",
                    EventSummary = "Position is solid"
                },
                new()
                {
                    TimestampUtc = baseTime.AddSeconds(3),
                    Kind = ReplayItemKind.ChatMessage,
                    MessageRole = "Assistant",
                    MessageContent = "The position looks good."
                }
            }
        };

        var sut = new BrainPromptBuilder();

        // Act
        var result = sut.AssembleReplayContext(context);

        // Assert -- header present
        result.Should().Contain("[REPLAY CONTEXT:");
        result.Should().Contain(baseTime.ToString("HH:mm:ss"));

        // Assert -- all items present in order
        var userIdx = result.IndexOf("User: What is happening?");
        var assessIdx = result.IndexOf("[Assessment] Position is solid");
        var assistIdx = result.IndexOf("Assistant: The position looks good.");

        userIdx.Should().BeGreaterThan(-1);
        assessIdx.Should().BeGreaterThan(userIdx);
        assistIdx.Should().BeGreaterThan(assessIdx);
    }

    [Fact]
    public void AssembleReplayContext_TruncatesLongContent()
    {
        // Arrange -- create many items that exceed 2400 chars
        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);
        var items = new List<ReplayItem>();
        for (int i = 0; i < 50; i++)
        {
            items.Add(new ReplayItem
            {
                TimestampUtc = baseTime.AddSeconds(i),
                Kind = ReplayItemKind.ChatMessage,
                MessageRole = "User",
                MessageContent = new string('A', 100) // 100 chars per message
            });
        }

        var context = new ReplayContext
        {
            SessionId = "sess-trunc",
            WindowStartUtc = baseTime,
            WindowEndUtc = baseTime.AddMinutes(1),
            Items = items
        };

        var sut = new BrainPromptBuilder();

        // Act
        var result = sut.AssembleReplayContext(context, maxTokenBudget: 600);

        // Assert -- truncated and shows truncation marker
        result.Length.Should().BeLessOrEqualTo(2500); // 600 tokens * 4 chars + some tolerance for marker
        result.Should().Contain("(truncated, showing most recent)");
    }

    [Fact]
    public void AssembleReplayContext_EmptyContext_ReturnsMinimalHeader()
    {
        // Arrange
        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);
        var context = new ReplayContext
        {
            SessionId = "sess-empty",
            WindowStartUtc = baseTime,
            WindowEndUtc = baseTime.AddMinutes(1),
            Items = Array.Empty<ReplayItem>()
        };

        var sut = new BrainPromptBuilder();

        // Act
        var result = sut.AssembleReplayContext(context);

        // Assert -- just header, no item lines
        result.Should().Contain("[REPLAY CONTEXT:");
        result.Trim().Split('\n').Length.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public void AssembleReplayContext_IncludesBrainSignal()
    {
        // Arrange
        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);
        var context = new ReplayContext
        {
            SessionId = "sess-signal",
            WindowStartUtc = baseTime,
            WindowEndUtc = baseTime.AddMinutes(1),
            Items = new List<ReplayItem>
            {
                new()
                {
                    TimestampUtc = baseTime.AddSeconds(1),
                    Kind = ReplayItemKind.TimelineEvent,
                    EventType = "Danger",
                    EventSummary = "Knight fork threatens queen",
                    BrainSignal = "danger"
                }
            }
        };

        var sut = new BrainPromptBuilder();

        // Act
        var result = sut.AssembleReplayContext(context);

        // Assert -- brain signal appears in output
        result.Should().Contain("danger");
        result.Should().Contain("[Danger]");
        result.Should().Contain("Knight fork threatens queen");
    }
}
