using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Timeline;

// ==========================================================================
// FLAT LIST BASICS
// ==========================================================================

public class TimelineFeedTests
{
    private TimelineFeed CreateSut(TimeSpan? retention = null, int sweepMs = 60_000) =>
        new(retention, sweepMs);

    [Fact]
    public void Events_StartsEmpty()
    {
        var sut = CreateSut();
        sut.Events.Should().BeEmpty();
    }

    [Fact]
    public void AddEvent_InsertsAtIndexZero()
    {
        var sut = CreateSut();

        var first = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First" };
        var second = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second" };

        sut.AddEvent(first);
        sut.AddEvent(second);

        sut.Events[0].Should().BeSameAs(second, "newest at index 0");
        sut.Events[1].Should().BeSameAs(first);
    }

    [Fact]
    public void AddEvent_SetsIsLatestOnNewest_ClearsPrevious()
    {
        var sut = CreateSut();

        var first = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First" };
        var second = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second" };

        sut.AddEvent(first);
        first.IsLatest.Should().BeTrue();
        first.IsExpanded.Should().BeTrue();

        sut.AddEvent(second);
        second.IsLatest.Should().BeTrue();
        second.IsExpanded.Should().BeTrue();
        first.IsLatest.Should().BeFalse("previous event loses IsLatest");
    }

    [Fact]
    public void AddEvent_DirectMessage_AlwaysExpanded()
    {
        var sut = CreateSut();

        var chat = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = "Hello",
            Role = MessageRole.User
        };

        sut.AddEvent(chat);

        chat.IsExpanded.Should().BeTrue("direct chat is always expanded");
    }

    [Fact]
    public void AddEvent_DirectMessage_DoesNotAffectIsLatest()
    {
        var sut = CreateSut();

        var nonChat = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice" };
        sut.AddEvent(nonChat);
        nonChat.IsLatest.Should().BeTrue();

        var chat = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = "Hello",
            Role = MessageRole.User
        };
        sut.AddEvent(chat);

        nonChat.IsLatest.Should().BeTrue("chat does not participate in IsLatest tracking");
    }

    [Fact]
    public void AddEvent_FiresEventAddedEvent()
    {
        var sut = CreateSut();
        TimelineEvent? received = null;
        sut.EventAdded += (_, evt) => received = evt;

        var added = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        sut.AddEvent(added);

        received.Should().BeSameAs(added);
    }

    [Fact]
    public void Clear_EmptiesTheList()
    {
        var sut = CreateSut();

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "A" });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "B" });

        sut.Clear();

        sut.Events.Should().BeEmpty();
    }

    [Fact]
    public void Clear_ResetsLatestTracking()
    {
        var sut = CreateSut();

        var first = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Before" };
        sut.AddEvent(first);

        sut.Clear();

        var afterClear = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "After" };
        sut.AddEvent(afterClear);

        afterClear.IsLatest.Should().BeTrue();
        afterClear.IsExpanded.Should().BeTrue();
    }
}

// ==========================================================================
// RETENTION + ARCHIVE BOUNDARY
// ==========================================================================

public class TimelineFeed_RetentionTests
{
    private TimelineFeed CreateSut(TimeSpan? retention = null, int sweepMs = 60_000) =>
        new(retention, sweepMs);

    [Fact]
    public void EnforceRetention_RemovesEventsOlderThanWindow()
    {
        var sut = CreateSut(retention: TimeSpan.FromMinutes(1));

        var old = new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "Old",
            Timestamp = DateTime.UtcNow.AddMinutes(-2)
        };
        var recent = new TimelineEvent
        {
            Type = EventOutputType.Danger,
            Summary = "Recent",
            Timestamp = DateTime.UtcNow
        };

        sut.AddEvent(old);
        sut.AddEvent(recent);

        sut.EnforceRetention();

        sut.Events.Should().Contain(recent);
        sut.Events.Should().NotContain(old);
    }

    [Fact]
    public void EnforceRetention_InsertsArchiveSentinel_WhenEventsRemoved()
    {
        var sut = CreateSut(retention: TimeSpan.FromMinutes(1));

        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "Old",
            Timestamp = DateTime.UtcNow.AddMinutes(-2)
        });
        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.Danger,
            Summary = "Recent",
            Timestamp = DateTime.UtcNow
        });

        sut.EnforceRetention();

        sut.Events.Last().Type.Should().Be(EventOutputType.Archived, "archive sentinel at tail");
    }

    [Fact]
    public void EnforceRetention_DoesNotInsertArchive_WhenNothingRemoved()
    {
        var sut = CreateSut(retention: TimeSpan.FromMinutes(5));

        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "Fresh",
            Timestamp = DateTime.UtcNow
        });

        sut.EnforceRetention();

        sut.Events.Should().NotContain(e => e.Type == EventOutputType.Archived);
    }

    [Fact]
    public void EnforceRetention_ReplacesExistingArchiveSentinel()
    {
        var sut = CreateSut(retention: TimeSpan.FromMinutes(1));

        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "Old1",
            Timestamp = DateTime.UtcNow.AddMinutes(-3)
        });
        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.Danger,
            Summary = "Recent",
            Timestamp = DateTime.UtcNow
        });

        sut.EnforceRetention(); // inserts archive sentinel
        sut.EnforceRetention(); // should replace, not duplicate

        sut.Events.Count(e => e.Type == EventOutputType.Archived).Should().Be(1);
    }

    [Fact]
    public void EnforceRetention_ClearsLatestRef_WhenLatestIsPruned()
    {
        var sut = CreateSut(retention: TimeSpan.FromMinutes(1));

        var oldEvent = new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "Old",
            Timestamp = DateTime.UtcNow.AddMinutes(-2)
        };
        sut.AddEvent(oldEvent);
        oldEvent.IsLatest.Should().BeTrue();

        sut.EnforceRetention();

        // After pruning the only non-chat event, adding a new one should get IsLatest
        var newEvent = new TimelineEvent { Type = EventOutputType.Danger, Summary = "New" };
        sut.AddEvent(newEvent);
        newEvent.IsLatest.Should().BeTrue();
    }

    [Fact]
    public void InsertArchiveBoundary_AddsArchivedEventAtEnd()
    {
        var sut = CreateSut();
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Live" });

        sut.InsertArchiveBoundary();

        sut.Events.Should().HaveCount(2);
        sut.Events.Last().Type.Should().Be(EventOutputType.Archived);
    }

    [Fact]
    public void InsertArchiveBoundary_ReplacesExistingBoundary()
    {
        var sut = CreateSut();
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Live" });

        sut.InsertArchiveBoundary();
        sut.InsertArchiveBoundary();

        sut.Events.Count(e => e.Type == EventOutputType.Archived).Should().Be(1);
    }
}

// ==========================================================================
// FOCUS COMPRESSION (simplified — no buckets)
// ==========================================================================

public class TimelineFeed_FocusCompressionTests
{
    private TimelineFeed CreateSut() => new();

    [Fact]
    public void DirectMessage_AlwaysExpanded()
    {
        var sut = CreateSut();

        var user = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = "Hello",
            Role = MessageRole.User
        };
        var ai = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = "Hi there",
            Role = MessageRole.Assistant
        };

        sut.AddEvent(user);
        sut.AddEvent(ai);

        user.IsExpanded.Should().BeTrue("direct chat is always expanded");
        ai.IsExpanded.Should().BeTrue("direct chat is always expanded");
    }

    [Fact]
    public void DirectMessage_CannotBeCollapsed_ByToggle()
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = "Chat",
            Role = MessageRole.User
        };
        evt.IsExpanded = true;
        evt.IsLatest = false;

        evt.ToggleExpandedCommand.Execute(null);

        evt.IsExpanded.Should().BeTrue("direct chat toggle is a no-op");
    }

    [Fact]
    public void AddEvent_NonChat_SetsIsLatest()
    {
        var sut = CreateSut();

        var first = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First" };
        var second = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second" };

        sut.AddEvent(first);
        first.IsLatest.Should().BeTrue();

        sut.AddEvent(second);
        first.IsLatest.Should().BeFalse("previous non-chat loses latest status");
        second.IsLatest.Should().BeTrue("new non-chat becomes latest");
    }

    [Fact]
    public void ToggleExpandedCommand_LatestEvent_CannotBeCollapsed()
    {
        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        evt.IsExpanded = true;
        evt.IsLatest = true;

        evt.ToggleExpandedCommand.Execute(null);

        evt.IsExpanded.Should().BeTrue("latest event must always remain expanded");
    }

    [Fact]
    public void ToggleExpandedCommand_NonLatest_CanToggle()
    {
        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        evt.IsExpanded = true;
        evt.IsLatest = false;

        evt.ToggleExpandedCommand.Execute(null);
        evt.IsExpanded.Should().BeFalse();

        evt.ToggleExpandedCommand.Execute(null);
        evt.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void IsExpanded_RaisesPropertyChanged()
    {
        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        var changedProps = new List<string>();
        evt.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        evt.IsExpanded = true;

        changedProps.Should().Contain(nameof(TimelineEvent.IsExpanded));
        changedProps.Should().Contain(nameof(TimelineEvent.IsCollapsed));
    }
}
