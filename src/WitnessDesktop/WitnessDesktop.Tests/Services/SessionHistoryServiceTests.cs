using WitnessDesktop.Data;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services.History;

namespace WitnessDesktop.Tests.Services;

public class SessionHistoryServiceTests : IDisposable
{
    private readonly string _dbPath;

    public SessionHistoryServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"gaimer-hist-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-journal"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
    }

    [Fact]
    public async Task StartSession_CreatesSessionRecord()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);

        // Act
        await sut.StartSessionAsync("sess-abc123", "leroy", "chess", "lichess.org", "Lichess - Board", "firefox");

        // Assert — read back via DbContext
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var session = await ctx.Sessions.FindAsync("sess-abc123");
        session.Should().NotBeNull();
        session!.AgentKey.Should().Be("leroy");
        session.GameType.Should().Be("chess");
        session.ConnectorName.Should().Be("lichess.org");
        session.TargetWindowTitle.Should().Be("Lichess - Board");
        session.TargetProcessName.Should().Be("firefox");
        session.StartedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.EndedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task FinalizeSession_SetsEndedAtUtc()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync("sess-fin001", "wasp", "chess", "chess.com", null, null);

        // Act
        await sut.FinalizeSessionAsync("sess-fin001");

        // Assert
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var session = await ctx.Sessions.FindAsync("sess-fin001");
        session.Should().NotBeNull();
        session!.EndedAtUtc.Should().NotBeNull();
        session.EndedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PersistChatMessage_RoundTrips()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync("sess-chat01", null, null, null, null, null);

        var msg = new ChatMessage
        {
            Id = "msg-round01",
            Role = MessageRole.User,
            Intent = MessageIntent.LiveGameInfo,
            Content = "What should I play here?",
            Source = "typed",
            DeliveryState = DeliveryState.Sent,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await sut.PersistChatMessageAsync("sess-chat01", msg);

        // Assert
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var loaded = await ctx.ChatMessages.FindAsync("msg-round01");
        loaded.Should().NotBeNull();
        loaded!.SessionId.Should().Be("sess-chat01");
        loaded.Role.Should().Be("User");
        loaded.Intent.Should().Be("LiveGameInfo");
        loaded.Content.Should().Be("What should I play here?");
        loaded.Source.Should().Be("typed");
        loaded.DeliveryState.Should().Be("Sent");
    }

    [Fact]
    public async Task PersistTimelineEvent_WithBrainMetadata_FlattensFields()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync("sess-brain01", null, null, null, null, null);

        var evt = new TimelineEvent
        {
            Id = "evt-brain01",
            Type = EventOutputType.Assessment,
            Summary = "Position is equal",
            FullContent = "The position is roughly equal with chances for both sides.",
            Brain = new BrainMetadata
            {
                Signal = "assessment",
                Urgency = "low",
                Evaluation = 15,
                EvalDelta = -3,
                SuggestedAction = "Develop the bishop to c4"
            }
        };

        // Act
        await sut.PersistTimelineEventAsync("sess-brain01", evt, checkpointId: null, displayOrder: 0);

        // Assert
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var loaded = await ctx.TimelineEvents.FindAsync("evt-brain01");
        loaded.Should().NotBeNull();
        loaded!.BrainSignal.Should().Be("assessment");
        loaded.BrainUrgency.Should().Be("low");
        loaded.BrainEvaluation.Should().Be(15);
        loaded.BrainEvalDelta.Should().Be(-3);
        loaded.SuggestedAction.Should().Be("Develop the bishop to c4");
    }

    [Fact]
    public async Task PersistTimelineEvent_WithToolCall_FlattensFields()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync("sess-tool01", null, null, null, null, null);

        var evt = new TimelineEvent
        {
            Id = "evt-tool01",
            Type = EventOutputType.ToolCall,
            Summary = "Stockfish analysis",
            ToolCall = new ToolCallInfo
            {
                ToolName = "analyze_position",
                DurationMs = 1250,
                Success = true
            }
        };

        // Act — pass null for checkpointId (no checkpoint records created from managed code)
        await sut.PersistTimelineEventAsync("sess-tool01", evt, checkpointId: null, displayOrder: 3);

        // Assert
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var loaded = await ctx.TimelineEvents.FindAsync("evt-tool01");
        loaded.Should().NotBeNull();
        loaded!.ToolName.Should().Be("analyze_position");
        loaded.ToolDurationMs.Should().Be(1250);
        loaded.CheckpointId.Should().BeNull();
        loaded.DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task WriteFailure_DoesNotThrow()
    {
        // Arrange — use an invalid path that cannot be created
        var badPath = "/nonexistent/deeply/nested/dir/test.db";
        var sut = new SessionHistoryService(badPath);

        // Act & Assert — none of these should throw
        var ex = await Record.ExceptionAsync(() =>
            sut.StartSessionAsync("fail-sess", null, null, null, null, null));
        ex.Should().BeNull();

        ex = await Record.ExceptionAsync(() =>
            sut.FinalizeSessionAsync("fail-sess"));
        ex.Should().BeNull();

        var msg = new ChatMessage { Content = "hello" };
        ex = await Record.ExceptionAsync(() =>
            sut.PersistChatMessageAsync("fail-sess", msg));
        ex.Should().BeNull();
    }

    // ── Session-ready gate tests ──────────────────────────────────────

    [Fact]
    public async Task ChildWrite_AfterStartSession_Succeeds()
    {
        // Arrange — start session, then write a child record
        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync("sess-gate01", null, null, null, null, null);

        var msg = new ChatMessage
        {
            Id = "msg-gate01",
            Role = MessageRole.User,
            Intent = MessageIntent.GeneralChat,
            Content = "Gate test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await sut.PersistChatMessageAsync("sess-gate01", msg);

        // Assert — child record exists
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var loaded = await ctx.ChatMessages.FindAsync("msg-gate01");
        loaded.Should().NotBeNull();
        loaded!.Content.Should().Be("Gate test");
    }

    [Fact]
    public async Task ChildWrite_WithoutStartSession_DoesNotThrow_AndSkipsWrite()
    {
        // Arrange — create service but NEVER call StartSessionAsync
        var sut = new SessionHistoryService(_dbPath);

        var msg = new ChatMessage
        {
            Id = "msg-nogate01",
            Role = MessageRole.User,
            Intent = MessageIntent.GeneralChat,
            Content = "Should be skipped",
            Timestamp = DateTime.UtcNow
        };

        // Act — should not throw
        var ex = await Record.ExceptionAsync(() =>
            sut.PersistChatMessageAsync("sess-nogate", msg));
        ex.Should().BeNull();

        // Assert — message was NOT persisted (no session row, gate returned false)
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var loaded = await ctx.ChatMessages.FindAsync("msg-nogate01");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentChildWrites_WaitForSession_ThenSucceed()
    {
        // Arrange — simulate production: StartSessionAsync is called fire-and-forget
        // (sets the gate immediately), then child writes arrive before it completes.
        var sut = new SessionHistoryService(_dbPath);

        // Fire StartSession (do NOT await — simulates fire-and-forget)
        var startTask = sut.StartSessionAsync("sess-race01", null, null, null, null, null);

        // Child writes fire immediately after — they should await the gate
        var writeTask1 = sut.PersistChatMessageAsync("sess-race01", new ChatMessage
        {
            Id = "msg-race01",
            Role = MessageRole.User,
            Intent = MessageIntent.GeneralChat,
            Content = "Race message 1",
            Timestamp = DateTime.UtcNow
        });

        var writeTask2 = sut.PersistTimelineEventAsync("sess-race01", new TimelineEvent
        {
            Id = "evt-race01",
            Type = EventOutputType.SageAdvice,
            Summary = "Race event 1",
            Timestamp = DateTime.UtcNow
        }, null, 0);

        // Wait for everything to complete
        await Task.WhenAll(startTask, writeTask1, writeTask2);

        // Assert — both child records persisted successfully
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var msg = await ctx.ChatMessages.FindAsync("msg-race01");
        msg.Should().NotBeNull();
        msg!.Content.Should().Be("Race message 1");

        var evt = await ctx.TimelineEvents.FindAsync("evt-race01");
        evt.Should().NotBeNull();
    }

    [Fact]
    public async Task FinalizeSession_WaitsForSessionReady()
    {
        // Arrange
        var sut = new SessionHistoryService(_dbPath);
        await sut.StartSessionAsync("sess-fingate", null, null, null, null, null);

        // Act
        await sut.FinalizeSessionAsync("sess-fingate");

        // Assert
        using var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        var session = await ctx.Sessions.FindAsync("sess-fingate");
        session.Should().NotBeNull();
        session!.EndedAtUtc.Should().NotBeNull();
    }
}
