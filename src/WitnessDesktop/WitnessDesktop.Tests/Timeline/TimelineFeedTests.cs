using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.History;

namespace WitnessDesktop.Tests.Timeline;

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
    public void NewCapture_DoesNotCreateCheckpoint()
    {
        var sut = CreateSut();

        sut.NewCapture("screenshot-001.png", TimeSpan.FromMinutes(3), "auto");

        sut.Checkpoints.Should().BeEmpty("captures are silent infrastructure");
    }

    [Fact]
    public void NewConversationCheckpoint_CreatesOutGameCheckpoint()
    {
        var sut = CreateSut();

        var checkpoint = sut.NewConversationCheckpoint();

        checkpoint.Context.Should().Be(SessionState.OutGame);
        checkpoint.BucketMinute.Should().BeCloseTo(
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day,
                DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddEvent_CreatesBucketOnFirstEvent()
    {
        var sut = CreateSut();

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Good move" });

        sut.Checkpoints.Should().HaveCount(1);
        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(1);
        sut.CurrentCheckpoint.EventLines[0].Events[0].Summary.Should().Be("Good move");
    }

    [Fact]
    public void AddEvent_SameMinute_ReusesBucket()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 10, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddSeconds(30) });

        sut.Checkpoints.Should().HaveCount(1, "both events are in the same minute");
    }

    [Fact]
    public void AddEvent_DifferentMinute_CreatesNewBucket()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddMinutes(1) });

        sut.Checkpoints.Should().HaveCount(2, "events are in different minutes");
        sut.Checkpoints[0].BucketMinute.Minute.Should().Be(now.AddMinutes(1).Minute); // newest first
    }

    [Fact]
    public void AddEvent_CurrentBucket_NonChat_DoesNotGroup()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "First danger", Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second danger", Timestamp = now.AddSeconds(10) });

        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(2, "current bucket: one row per output, no grouping");
        sut.CurrentCheckpoint.EventLines[0].Events.Should().HaveCount(1);
        sut.CurrentCheckpoint.EventLines[1].Events.Should().HaveCount(1);
    }

    [Fact]
    public void OlderBucket_NonChat_GroupsSameOutputType()
    {
        var sut = CreateSut();
        var oldTime = new DateTime(2026, 3, 16, 10, 10, 0, DateTimeKind.Utc);
        var newTime = oldTime.AddMinutes(2);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "First danger", Timestamp = oldTime });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second danger", Timestamp = oldTime.AddSeconds(10) });

        // Trigger compression by creating a new bucket
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "New", Timestamp = newTime });

        var olderBucket = sut.Checkpoints[1]; // second checkpoint = older bucket
        olderBucket.EventLines.Should().HaveCount(1, "older bucket merges same-type non-chat into one line");
        olderBucket.EventLines[0].Events.Should().HaveCount(2);
    }

    [Fact]
    public void OlderBucket_Compression_PreservesNewestFirstChronologyAcrossChatAndEvents()
    {
        var sut = CreateSut();
        var oldTime = new DateTime(2026, 3, 16, 10, 10, 0, DateTimeKind.Utc);
        var newTime = oldTime.AddMinutes(2);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Danger 1", Timestamp = oldTime.AddSeconds(5) });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "Chat", FullContent = "Chat", Role = MessageRole.User, Timestamp = oldTime.AddSeconds(20) });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Danger 2", Timestamp = oldTime.AddSeconds(40) });

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "New bucket", Timestamp = newTime });

        var olderBucket = sut.Checkpoints[1];
        olderBucket.EventLines.Should().HaveCount(2);
        olderBucket.EventLines[0].IsDirectChat.Should().BeFalse("the newest line in the old minute is the merged danger group");
        olderBucket.EventLines[0].Events.Should().HaveCount(2);
        olderBucket.EventLines[1].IsDirectChat.Should().BeTrue("the older chat line should remain after the newer danger group");
    }

    [Fact]
    public void AddEvent_DirectMessage_NeverGroups()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "User msg", Role = MessageRole.User, Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "AI reply", Role = MessageRole.Assistant, Timestamp = now.AddSeconds(5) });

        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(2, "each DirectMessage gets its own EventLine");
        sut.CurrentCheckpoint.EventLines[0].Events.Should().HaveCount(1);
        sut.CurrentCheckpoint.EventLines[1].Events.Should().HaveCount(1);
    }

    [Fact]
    public void AddEvent_DifferentNonChatType_CreatesNewEventLine()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Danger", Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Opportunity, Summary = "Opportunity", Timestamp = now.AddSeconds(5) });

        sut.CurrentCheckpoint!.EventLines.Should().HaveCount(2);
    }

    [Fact]
    public void CheckpointCreated_EventFires_OnBucketCreation()
    {
        var sut = CreateSut();
        TimelineCheckpoint? received = null;
        sut.CheckpointCreated += (_, cp) => received = cp;

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" });

        received.Should().NotBeNull();
    }

    [Fact]
    public void CurrentCheckpoint_SkipsArchiveBoundary()
    {
        var sut = CreateSut();
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Live" });
        sut.InsertArchiveBoundary();

        sut.CurrentCheckpoint!.IsArchiveBoundary.Should().BeFalse();
    }

    [Fact]
    public void CompressOlderBucket_NewestEventIsFirstInMergedLine()
    {
        var sut = CreateSut();
        var baseTime = new DateTime(2026, 3, 21, 10, 0, 0, DateTimeKind.Utc);

        // Add 3 same-type events in the same minute bucket
        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.Danger,
            Summary = "First threat",
            Timestamp = baseTime,
        });
        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.Danger,
            Summary = "Second threat",
            Timestamp = baseTime.AddSeconds(10),
        });
        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.Danger,
            Summary = "Third threat",
            Timestamp = baseTime.AddSeconds(20),
        });

        // Trigger compression by adding an event in a new minute
        sut.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "New minute event",
            Timestamp = baseTime.AddMinutes(2),
        });

        // Find the compressed bucket (not the current one)
        var compressedBucket = sut.Checkpoints.FirstOrDefault(c =>
            !c.IsArchiveBoundary && c != sut.CurrentCheckpoint);
        compressedBucket.Should().NotBeNull();

        var dangerLine = compressedBucket!.EventLines
            .FirstOrDefault(l => l.OutputType == EventOutputType.Danger);
        dangerLine.Should().NotBeNull();

        // Newest should be first (index 0) and expanded
        dangerLine!.Events[0].Summary.Should().Be("Third threat");
        dangerLine.Events[0].IsExpanded.Should().BeTrue();

        // Older events should be collapsed
        dangerLine.Events[1].IsExpanded.Should().BeFalse();
        dangerLine.Events[2].IsExpanded.Should().BeFalse();
    }
}

// ==========================================================================
// FOCUS COMPRESSION
// ==========================================================================

public class TimelineFeed_FocusCompressionTests
{
    private readonly Mock<ISessionManager> _mockSession;

    public TimelineFeed_FocusCompressionTests()
    {
        _mockSession = new Mock<ISessionManager>();
        _mockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);
    }

    private TimelineFeed CreateSut() => new(_mockSession.Object);

    // === DirectMessage: always expanded, never compresses ===

    [Fact]
    public void DirectMessage_AlwaysExpanded_InCurrentBucket()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        var user = new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "Hello", Role = MessageRole.User, Timestamp = now };
        var ai = new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "Hi there", Role = MessageRole.Assistant, Timestamp = now.AddSeconds(3) };

        sut.AddEvent(user);
        sut.AddEvent(ai);

        user.IsExpanded.Should().BeTrue("direct chat is always expanded");
        ai.IsExpanded.Should().BeTrue("direct chat is always expanded");
    }

    [Fact]
    public void DirectMessage_AlwaysExpanded_InOlderBucket()
    {
        var sut = CreateSut();
        var oldTime = new DateTime(2026, 3, 16, 10, 10, 0, DateTimeKind.Utc);
        var newTime = oldTime.AddMinutes(2);

        var oldChat = new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "Old msg", Role = MessageRole.User, Timestamp = oldTime };
        sut.AddEvent(oldChat);

        // Force a new bucket — the old bucket is now "older"
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "New event", Timestamp = newTime });

        oldChat.IsExpanded.Should().BeTrue("direct chat never collapses, even in older buckets");
    }

    [Fact]
    public void DirectMessage_CannotBeCollapsed_ByToggle()
    {
        var evt = new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "Chat", Role = MessageRole.User };
        evt.IsExpanded = true;
        evt.IsLatest = false;

        evt.ToggleExpandedCommand.Execute(null);

        evt.IsExpanded.Should().BeTrue("direct chat toggle is a no-op");
    }

    [Fact]
    public void DirectMessage_MixedWithNonChat_StaysExpanded_InOlderBucket()
    {
        var sut = CreateSut();
        var oldTime = new DateTime(2026, 3, 16, 10, 10, 0, DateTimeKind.Utc);
        var newTime = oldTime.AddMinutes(2);

        // Old bucket: chat + non-chat
        var chat = new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "User msg", Role = MessageRole.User, Timestamp = oldTime };
        var nonChat1 = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice 1", Timestamp = oldTime.AddSeconds(10) };
        var nonChat2 = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice 2", Timestamp = oldTime.AddSeconds(20) };
        sut.AddEvent(chat);
        sut.AddEvent(nonChat1);
        sut.AddEvent(nonChat2);

        // Trigger compression by creating a new bucket
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "New", Timestamp = newTime });

        chat.IsExpanded.Should().BeTrue("direct chat survives compression");
        nonChat1.IsExpanded.Should().BeFalse("older non-chat sibling gets compressed");
        nonChat2.IsExpanded.Should().BeTrue("newest non-chat in line stays expanded");
    }

    // === Current bucket: all non-chat events expanded ===

    [Fact]
    public void CurrentBucket_AllNonChatEvents_Expanded()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        var first = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now };
        var second = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddSeconds(10) };
        var third = new TimelineEvent { Type = EventOutputType.Opportunity, Summary = "Third", Timestamp = now.AddSeconds(20) };

        sut.AddEvent(first);
        sut.AddEvent(second);
        sut.AddEvent(third);

        first.IsExpanded.Should().BeTrue("all current-bucket events stay expanded");
        second.IsExpanded.Should().BeTrue("all current-bucket events stay expanded");
        third.IsExpanded.Should().BeTrue("all current-bucket events stay expanded");
    }

    // === Older bucket: non-chat events compress, newest stays expanded ===

    [Fact]
    public void OlderBucket_NonChat_CompressesAllButNewest()
    {
        var sut = CreateSut();
        var oldTime = new DateTime(2026, 3, 16, 10, 10, 0, DateTimeKind.Utc);
        var newTime = oldTime.AddMinutes(2);

        // Build up several non-chat events in the same bucket, same type (they group)
        var evt1 = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice 1", Timestamp = oldTime };
        var evt2 = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice 2", Timestamp = oldTime.AddSeconds(15) };
        var evt3 = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice 3", Timestamp = oldTime.AddSeconds(30) };
        sut.AddEvent(evt1);
        sut.AddEvent(evt2);
        sut.AddEvent(evt3);

        // All expanded while current
        evt1.IsExpanded.Should().BeTrue();
        evt2.IsExpanded.Should().BeTrue();
        evt3.IsExpanded.Should().BeTrue();

        // Create a new bucket — triggers compression of the old one
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "New bucket", Timestamp = newTime });

        evt1.IsExpanded.Should().BeFalse("oldest in group compresses");
        evt2.IsExpanded.Should().BeFalse("middle in group compresses");
        evt3.IsExpanded.Should().BeTrue("newest in group stays expanded");
    }

    // === IsLatest tracking ===

    [Fact]
    public void AddEvent_NonChat_SetsIsLatest()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        var first = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now };
        var second = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddSeconds(10) };

        sut.AddEvent(first);
        first.IsLatest.Should().BeTrue();

        sut.AddEvent(second);
        first.IsLatest.Should().BeFalse("previous non-chat loses latest status");
        second.IsLatest.Should().BeTrue("new non-chat becomes latest");
    }

    [Fact]
    public void AddEvent_DirectMessage_DoesNotAffectIsLatest()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        var nonChat = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Advice", Timestamp = now };
        sut.AddEvent(nonChat);
        nonChat.IsLatest.Should().BeTrue();

        var chat = new TimelineEvent { Type = EventOutputType.DirectMessage, Summary = "Hello", Role = MessageRole.User, Timestamp = now.AddSeconds(5) };
        sut.AddEvent(chat);

        nonChat.IsLatest.Should().BeTrue("chat doesn't participate in IsLatest tracking");
    }

    // === Manual expand doesn't interfere with compression tracking ===

    [Fact]
    public void ManualExpand_DoesNotAffectLatestTracking()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc);

        var first = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now };
        var second = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddSeconds(10) };

        sut.AddEvent(first);
        sut.AddEvent(second);

        // Both are expanded (current bucket keeps all expanded)
        first.IsExpanded.Should().BeTrue();
        second.IsExpanded.Should().BeTrue();

        // Manually collapse first, then re-expand it
        first.ToggleExpandedCommand.Execute(null); // toggles to collapsed
        first.IsExpanded.Should().BeFalse();
        first.ToggleExpandedCommand.Execute(null); // toggles back to expanded
        first.IsExpanded.Should().BeTrue("manually toggled back on");

        // Adding a third event: latestNonChat (second) loses IsLatest
        var third = new TimelineEvent { Type = EventOutputType.Assessment, Summary = "Third", Timestamp = now.AddSeconds(20) };
        sut.AddEvent(third);

        second.IsLatest.Should().BeFalse("tracked latest changes");
        third.IsLatest.Should().BeTrue("new latest");
        // first stays expanded because it was manually expanded
        first.IsExpanded.Should().BeTrue();
    }

    // === Toggle and property change mechanics ===

    [Fact]
    public void ToggleExpandedCommand_ExpandsCollapsedNonChatEvent()
    {
        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        evt.IsExpanded = false;

        evt.ToggleExpandedCommand.Execute(null);

        evt.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void ToggleExpandedCommand_CollapsesExpandedNonChatNonLatestEvent()
    {
        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        evt.IsExpanded = true;
        evt.IsLatest = false;

        evt.ToggleExpandedCommand.Execute(null);

        evt.IsExpanded.Should().BeFalse();
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
    public void IsExpanded_RaisesPropertyChanged()
    {
        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" };
        var changedProps = new List<string>();
        evt.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        evt.IsExpanded = true;

        changedProps.Should().Contain(nameof(TimelineEvent.IsExpanded));
        changedProps.Should().Contain(nameof(TimelineEvent.IsCollapsed));
    }

    [Fact]
    public void Clear_ResetsLatestTracking()
    {
        var sut = CreateSut();

        var first = new TimelineEvent { Type = EventOutputType.Danger, Summary = "Before clear" };
        sut.AddEvent(first);
        first.IsExpanded.Should().BeTrue();

        sut.Clear();

        var afterClear = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "After clear" };
        sut.AddEvent(afterClear);

        afterClear.IsExpanded.Should().BeTrue();
        first.IsExpanded.Should().BeTrue();
    }
}

// ==========================================================================
// ARCHIVE BOUNDARY MARKER
// ==========================================================================

public class TimelineFeed_ArchiveBoundaryTests
{
    private readonly Mock<ISessionManager> _mockSession;

    public TimelineFeed_ArchiveBoundaryTests()
    {
        _mockSession = new Mock<ISessionManager>();
        _mockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);
    }

    private TimelineFeed CreateSut() => new(_mockSession.Object);

    [Fact]
    public void InsertArchiveBoundary_AddsCheckpointAtEnd()
    {
        var sut = CreateSut();
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Live" });

        sut.InsertArchiveBoundary();

        sut.Checkpoints.Should().HaveCount(2);
        sut.Checkpoints.Last().IsArchiveBoundary.Should().BeTrue();
    }

    [Fact]
    public void InsertArchiveBoundary_IsGlobalNotTiedToCurrentCheckpoint()
    {
        var sut = CreateSut();
        var t1 = new DateTime(2026, 3, 16, 10, 10, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(2);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = t1 });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = t2 });

        sut.InsertArchiveBoundary();

        // Two real buckets + one archive boundary
        sut.Checkpoints.Should().HaveCount(3);
        sut.Checkpoints[0].IsArchiveBoundary.Should().BeFalse(); // newest bucket
        sut.Checkpoints[1].IsArchiveBoundary.Should().BeFalse(); // older bucket
        sut.Checkpoints[2].IsArchiveBoundary.Should().BeTrue();  // archive marker
    }

    [Fact]
    public void InsertArchiveBoundary_ReplacesExistingBoundary()
    {
        var sut = CreateSut();
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Live" });

        sut.InsertArchiveBoundary();
        sut.InsertArchiveBoundary();

        sut.Checkpoints.Count(c => c.IsArchiveBoundary).Should().Be(1);
        sut.Checkpoints.Last().IsArchiveBoundary.Should().BeTrue();
    }

    [Fact]
    public void InsertArchiveBoundary_DoesNotAffectLatestTracking()
    {
        var sut = CreateSut();

        var evt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Live event" };
        sut.AddEvent(evt);
        evt.IsExpanded.Should().BeTrue();

        sut.InsertArchiveBoundary();

        evt.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void ArchivedEvent_HasCorrectIcon()
    {
        var icon = EventIconMap.GetIcon(EventOutputType.Archived);
        icon.Should().Be("history_clock.png");
    }
}

public class TimelineCheckpointTests
{
    [Fact]
    public void ContextBadge_FormatsAsLocalTime()
    {
        var cp = new TimelineCheckpoint { Timestamp = DateTime.UtcNow };
        var expected = DateTime.UtcNow.ToLocalTime().ToString("h:mm tt").ToLowerInvariant();
        cp.ContextBadge.Should().Be(expected);
    }

    [Fact]
    public void ContextBadge_PastTimestamp_FormatsAsLocalTime()
    {
        var ts = DateTime.UtcNow.AddHours(-3);
        var cp = new TimelineCheckpoint { Timestamp = ts };
        var expected = ts.ToLocalTime().ToString("h:mm tt").ToLowerInvariant();
        cp.ContextBadge.Should().Be(expected);
    }
}

// ==========================================================================
// RETENTION ENGINE
// ==========================================================================

public class TimelineFeed_RetentionTests
{
    private readonly Mock<ISessionManager> _mockSession;

    public TimelineFeed_RetentionTests()
    {
        _mockSession = new Mock<ISessionManager>();
        _mockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);
    }

    /// <summary>
    /// Creates a TimelineFeed with a configurable retention window.
    /// sweepIntervalMs is set very high to prevent automatic sweeps during tests.
    /// </summary>
    private TimelineFeed CreateSut(TimeSpan? retentionWindow = null) =>
        new(_mockSession.Object, retentionWindow: retentionWindow ?? TimeSpan.FromMinutes(5), sweepIntervalMs: 600_000);

    [Fact]
    public void EnforceRetention_RemovesCheckpointsOlderThanWindow()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5));

        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        var recentTime = DateTime.UtcNow;

        // Add an old event (10 min ago) — creates old bucket
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Old", Timestamp = oldTime });

        // Add a recent event (now) — creates new bucket, compresses old
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Recent", Timestamp = recentTime });

        // Should have 2 checkpoints before retention
        sut.Checkpoints.Count(c => !c.IsArchiveBoundary).Should().Be(2);

        sut.EnforceRetention();

        // Old checkpoint should be pruned; only recent remains
        sut.Checkpoints.Count(c => !c.IsArchiveBoundary).Should().Be(1);
        sut.Checkpoints.First(c => !c.IsArchiveBoundary).EventLines[0].Events[0].Summary
            .Should().Be("Recent");
    }

    [Fact]
    public void EnforceRetention_PreservesEventsWithinWindow()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5));

        var recentTime1 = DateTime.UtcNow.AddMinutes(-2);
        var recentTime2 = DateTime.UtcNow;

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Recent1", Timestamp = recentTime1 });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Recent2", Timestamp = recentTime2 });

        sut.EnforceRetention();

        // Both should survive — within 5 min window
        sut.Checkpoints.Count(c => !c.IsArchiveBoundary).Should().Be(2);
    }

    [Fact]
    public void EnforceRetention_InsertsArchiveBoundaryWhenPruned()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5));

        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        var recentTime = DateTime.UtcNow;

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Old", Timestamp = oldTime });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Recent", Timestamp = recentTime });

        sut.EnforceRetention();

        sut.Checkpoints.Any(c => c.IsArchiveBoundary).Should().BeTrue(
            "an archive boundary should appear after pruning");
    }

    [Fact]
    public void EnforceRetention_CurrentCheckpointExemptEvenIfOld()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5));

        // Single old event — its bucket IS the CurrentCheckpoint
        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Only event", Timestamp = oldTime });

        sut.Checkpoints.Count(c => !c.IsArchiveBoundary).Should().Be(1);

        sut.EnforceRetention();

        // Current checkpoint must survive even though it's old
        sut.Checkpoints.Count(c => !c.IsArchiveBoundary).Should().Be(1);
        sut.CurrentCheckpoint!.EventLines[0].Events[0].Summary.Should().Be("Only event");
    }

    [Fact]
    public void EnforceRetention_OnArchiveBoundaryOnly_IsNoOp()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5));

        // Manually insert only an archive boundary (no real checkpoints)
        sut.InsertArchiveBoundary();

        sut.Checkpoints.Should().HaveCount(1);
        sut.Checkpoints[0].IsArchiveBoundary.Should().BeTrue();

        // Should not crash
        sut.EnforceRetention();

        sut.Checkpoints.Should().HaveCount(1);
    }

    [Fact]
    public void EnforceRetention_NullsLatestNonChatWhenOwningCheckpointPruned()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5));

        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        var recentTime = DateTime.UtcNow;

        // Add an old non-chat event that becomes _latestNonChatEvent
        var oldEvt = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Old latest", Timestamp = oldTime };
        sut.AddEvent(oldEvt);
        oldEvt.IsLatest.Should().BeTrue("it was the last non-chat event added");

        // Add a recent event in a new bucket — the old event KEEPS IsLatest because
        // the new event takes over. But we need the old to still be latest for the test.
        // Instead: add the old event, then a recent DirectMessage (which doesn't affect IsLatest)
        var sut2 = CreateSut(TimeSpan.FromMinutes(5));
        var oldEvt2 = new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Old latest", Timestamp = oldTime };
        sut2.AddEvent(oldEvt2);

        // Add a DirectMessage in a new bucket (doesn't affect IsLatest tracking)
        sut2.AddEvent(new TimelineEvent
        {
            Type = EventOutputType.DirectMessage,
            Summary = "Chat",
            FullContent = "Chat",
            Role = MessageRole.User,
            Timestamp = recentTime,
        });

        oldEvt2.IsLatest.Should().BeTrue("DirectMessage doesn't affect IsLatest");

        sut2.EnforceRetention();

        // After pruning the old checkpoint, the dangling _latestNonChatEvent should be cleared.
        // We verify by adding a new non-chat event and checking that the old one didn't
        // get its IsLatest cleared (since it was already nulled).
        var newEvt = new TimelineEvent { Type = EventOutputType.Danger, Summary = "New", Timestamp = DateTime.UtcNow.AddSeconds(1) };
        sut2.AddEvent(newEvt);
        newEvt.IsLatest.Should().BeTrue("new event becomes latest");
        // Old event's IsLatest should remain true (dangling reference was nulled, not toggled)
        oldEvt2.IsLatest.Should().BeTrue("pruned event's IsLatest was not toggled — reference was nulled");
    }
}

// ==========================================================================
// CHECKPOINT PERSISTENCE (Finding 5)
// ==========================================================================

public class TimelineFeed_CheckpointPersistenceTests
{
    private readonly Mock<ISessionManager> _mockSession;
    private readonly Mock<ISessionHistoryService> _mockHistory;
    private readonly Mock<ISessionTraceService> _mockTrace;

    public TimelineFeed_CheckpointPersistenceTests()
    {
        _mockSession = new Mock<ISessionManager>();
        _mockSession.Setup(s => s.CurrentState).Returns(SessionState.InGame);

        _mockHistory = new Mock<ISessionHistoryService>();
        _mockHistory
            .Setup(h => h.PersistTimelineCheckpointAsync(It.IsAny<string>(), It.IsAny<TimelineCheckpoint>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _mockTrace = new Mock<ISessionTraceService>();
        _mockTrace.Setup(t => t.SessionId).Returns("test-session-id");
    }

    private TimelineFeed CreateSut() =>
        new(_mockSession.Object, _mockTrace.Object, _mockHistory.Object);

    [Fact]
    public void AddEvent_CreatesNewBucket_PersistsCheckpoint()
    {
        var sut = CreateSut();

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" });

        _mockHistory.Verify(
            h => h.PersistTimelineCheckpointAsync("test-session-id", It.IsAny<TimelineCheckpoint>(), It.IsAny<int>()),
            Times.Once,
            "checkpoint should be persisted when a new bucket is created");
    }

    [Fact]
    public void AddEvent_SameMinute_DoesNotPersistAgain()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 28, 10, 15, 10, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddSeconds(30) });

        _mockHistory.Verify(
            h => h.PersistTimelineCheckpointAsync("test-session-id", It.IsAny<TimelineCheckpoint>(), It.IsAny<int>()),
            Times.Once,
            "reusing an existing bucket should not persist a second checkpoint");
    }

    [Fact]
    public void AddEvent_NewMinute_PersistsSecondCheckpoint()
    {
        var sut = CreateSut();
        var now = new DateTime(2026, 3, 28, 10, 15, 0, DateTimeKind.Utc);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "First", Timestamp = now });
        sut.AddEvent(new TimelineEvent { Type = EventOutputType.Danger, Summary = "Second", Timestamp = now.AddMinutes(1) });

        _mockHistory.Verify(
            h => h.PersistTimelineCheckpointAsync("test-session-id", It.IsAny<TimelineCheckpoint>(), It.IsAny<int>()),
            Times.Exactly(2),
            "each new minute bucket should persist its checkpoint");
    }

    [Fact]
    public void NewConversationCheckpoint_PersistsCheckpoint()
    {
        var sut = CreateSut();

        sut.NewConversationCheckpoint();

        _mockHistory.Verify(
            h => h.PersistTimelineCheckpointAsync("test-session-id", It.IsAny<TimelineCheckpoint>(), It.IsAny<int>()),
            Times.Once,
            "conversation checkpoint should be persisted");
    }

    [Fact]
    public void NoSessionActive_DoesNotPersist()
    {
        var noTraceMock = new Mock<ISessionTraceService>();
        noTraceMock.Setup(t => t.SessionId).Returns((string?)null);

        var sut = new TimelineFeed(_mockSession.Object, noTraceMock.Object, _mockHistory.Object);

        sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" });

        _mockHistory.Verify(
            h => h.PersistTimelineCheckpointAsync(It.IsAny<string>(), It.IsAny<TimelineCheckpoint>(), It.IsAny<int>()),
            Times.Never,
            "should not persist when no session is active");
    }

    [Fact]
    public void NoHistoryService_DoesNotThrow()
    {
        // TimelineFeed with no history service (null) should work fine
        var sut = new TimelineFeed(_mockSession.Object, _mockTrace.Object, historyService: null);

        var act = () => sut.AddEvent(new TimelineEvent { Type = EventOutputType.SageAdvice, Summary = "Test" });

        act.Should().NotThrow("missing history service should be silently ignored");
    }
}
