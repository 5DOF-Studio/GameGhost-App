using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class BrainContextFormatterTests
{
    [Fact]
    public void FormatL1Events_EmptyList_ReturnsNoObservations()
    {
        var result = BrainContextFormatter.FormatL1Events(Array.Empty<BrainEvent>());
        result.Should().Be("No recent observations.");
    }

    [Fact]
    public void FormatL1Events_SingleEvent_FormatsAsTimestampCategoryText()
    {
        var events = new[]
        {
            new BrainEvent
            {
                TimestampUtc = new DateTime(2026, 4, 7, 14, 30, 15, DateTimeKind.Utc),
                Category = "threat",
                Text = "Opponent queen on d7 targets f5 pawn"
            }
        };

        var result = BrainContextFormatter.FormatL1Events(events);
        result.Should().Be("[14:30:15] threat: Opponent queen on d7 targets f5 pawn");
    }

    [Fact]
    public void FormatL1Events_MultipleEvents_JoinsWithNewlines()
    {
        var events = new[]
        {
            new BrainEvent
            {
                TimestampUtc = new DateTime(2026, 4, 7, 14, 30, 15, DateTimeKind.Utc),
                Category = "position",
                Text = "Closed position, equal evaluation"
            },
            new BrainEvent
            {
                TimestampUtc = new DateTime(2026, 4, 7, 14, 30, 20, DateTimeKind.Utc),
                Category = "threat",
                Text = "Knight fork possible on c7"
            }
        };

        var result = BrainContextFormatter.FormatL1Events(events);
        result.Should().Contain("[14:30:15] position: Closed position, equal evaluation");
        result.Should().Contain("[14:30:20] threat: Knight fork possible on c7");
    }

    [Fact]
    public void FormatL1Events_MoreThanMaxEvents_TakesOnlyMax()
    {
        var events = Enumerable.Range(0, 10).Select(i => new BrainEvent
        {
            TimestampUtc = new DateTime(2026, 4, 7, 14, 30, i, DateTimeKind.Utc),
            Category = "obs",
            Text = $"Event {i}"
        }).ToArray();

        var result = BrainContextFormatter.FormatL1Events(events, maxEvents: 5);
        result.Split('\n').Should().HaveCount(5);
    }

    [Fact]
    public void FormatRecentActivity_BothEmpty_ReturnsNull()
    {
        var result = BrainContextFormatter.FormatRecentActivity("", "");
        result.Should().BeNull();
    }

    [Fact]
    public void FormatRecentActivity_ChatOnly_ReturnsChatSection()
    {
        var result = BrainContextFormatter.FormatRecentActivity("User asked about openings", "");
        result.Should().Contain("Recent Chat");
        result.Should().Contain("User asked about openings");
        result.Should().NotContain("Recent Voice");
    }

    [Fact]
    public void FormatRecentActivity_Both_ReturnsCombined()
    {
        var result = BrainContextFormatter.FormatRecentActivity(
            "User asked about openings",
            "User (voice): What's the best move here?");
        result.Should().Contain("Recent Chat");
        result.Should().Contain("Recent Voice");
    }
}
