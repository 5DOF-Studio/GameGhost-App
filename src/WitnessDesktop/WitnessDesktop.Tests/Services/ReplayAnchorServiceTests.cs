using WitnessDesktop.Data;
using WitnessDesktop.Data.Entities;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Services;

public sealed class ReplayAnchorServiceTests : IDisposable
{
    private readonly string _dbPath;

    public ReplayAnchorServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "gaimer-anchor-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    public void Dispose()
    {
        foreach (var ext in new[] { "", "-journal", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + ext); } catch { }
        }
        try { Directory.Delete(Path.GetDirectoryName(_dbPath)!, true); } catch { }
    }

    private GaimerHistoryDbContext CreateContext()
    {
        var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private void SeedSession(string sessionId)
    {
        using var ctx = CreateContext();
        if (ctx.Sessions.Find(sessionId) == null)
        {
            ctx.Sessions.Add(new SessionRecord
            {
                SessionId = sessionId,
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            });
            ctx.SaveChanges();
        }
    }

    [Fact]
    public async Task GetAroundEvent_ExpandsTimeWindow()
    {
        // Arrange
        const string sessionId = "sess-evt-anchor";
        SeedSession(sessionId);

        var eventTime = new DateTime(2026, 3, 28, 12, 0, 30, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.TimelineEvents.Add(new TimelineEventRecord
            {
                Id = "evt-anchor", SessionId = sessionId, CreatedAtUtc = eventTime,
                Type = "Assessment", Summary = "Anchor event", DisplayOrder = 0
            });
            // Message within radius
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-near", SessionId = sessionId,
                TimestampUtc = eventTime.AddSeconds(-5),
                Role = "User", Content = "Nearby message"
            });
            // Message outside radius
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-far", SessionId = sessionId,
                TimestampUtc = eventTime.AddSeconds(-20),
                Role = "User", Content = "Far message"
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act -- 10 second radius around the event
        var result = await sut.GetAroundEventAsync(sessionId, "evt-anchor", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().Contain(i => i.EventSummary == "Anchor event");
        result.Items.Should().Contain(i => i.MessageContent == "Nearby message");
        result.Items.Should().NotContain(i => i.MessageContent == "Far message");
    }

    [Fact]
    public async Task GetAroundEvent_UnknownId_ReturnsNull()
    {
        // Arrange
        const string sessionId = "sess-evt-null";
        SeedSession(sessionId);

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act
        var result = await sut.GetAroundEventAsync(sessionId, "nonexistent-event", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAroundMessage_ExpandsTimeWindow()
    {
        // Arrange
        const string sessionId = "sess-msg-anchor";
        SeedSession(sessionId);

        var messageTime = new DateTime(2026, 3, 28, 12, 1, 0, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-anchor", SessionId = sessionId,
                TimestampUtc = messageTime,
                Role = "User", Content = "Anchor message"
            });
            // Event within radius
            ctx.TimelineEvents.Add(new TimelineEventRecord
            {
                Id = "evt-near", SessionId = sessionId,
                CreatedAtUtc = messageTime.AddSeconds(5),
                Type = "SageAdvice", Summary = "Nearby event", DisplayOrder = 0
            });
            // Event outside radius
            ctx.TimelineEvents.Add(new TimelineEventRecord
            {
                Id = "evt-far", SessionId = sessionId,
                CreatedAtUtc = messageTime.AddSeconds(30),
                Type = "Assessment", Summary = "Far event", DisplayOrder = 1
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act -- 15 second radius around the message
        var result = await sut.GetAroundMessageAsync(sessionId, "msg-anchor", TimeSpan.FromSeconds(15));

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().Contain(i => i.MessageContent == "Anchor message");
        result.Items.Should().Contain(i => i.EventSummary == "Nearby event");
        result.Items.Should().NotContain(i => i.EventSummary == "Far event");
    }

    [Fact]
    public async Task GetAroundMessage_UnknownId_ReturnsNull()
    {
        // Arrange
        const string sessionId = "sess-msg-null";
        SeedSession(sessionId);

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act
        var result = await sut.GetAroundMessageAsync(sessionId, "nonexistent-message", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().BeNull();
    }

    // ================================================================
    // Finding 3: Session ID validation — cross-session anchor safety
    // ================================================================

    [Fact]
    public async Task GetAroundEvent_WrongSession_ReturnsNull()
    {
        // Arrange — event exists but belongs to a different session
        const string sessionA = "sess-a";
        const string sessionB = "sess-b";
        SeedSession(sessionA);
        SeedSession(sessionB);

        var eventTime = new DateTime(2026, 3, 28, 12, 0, 30, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.TimelineEvents.Add(new TimelineEventRecord
            {
                Id = "evt-session-a", SessionId = sessionA, CreatedAtUtc = eventTime,
                Type = "Assessment", Summary = "Session A event", DisplayOrder = 0
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act — query with sessionB but event belongs to sessionA
        var result = await sut.GetAroundEventAsync(sessionB, "evt-session-a", TimeSpan.FromSeconds(10));

        // Assert — should return null because the event belongs to a different session
        result.Should().BeNull("the event belongs to session A, not session B");
    }

    [Fact]
    public async Task GetAroundEvent_CorrectSession_ReturnsContext()
    {
        // Arrange
        const string sessionId = "sess-correct-evt";
        SeedSession(sessionId);

        var eventTime = new DateTime(2026, 3, 28, 12, 0, 30, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.TimelineEvents.Add(new TimelineEventRecord
            {
                Id = "evt-correct", SessionId = sessionId, CreatedAtUtc = eventTime,
                Type = "Assessment", Summary = "Correct session event", DisplayOrder = 0
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act — query with the correct session
        var result = await sut.GetAroundEventAsync(sessionId, "evt-correct", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().NotBeNull("the event belongs to the requested session");
        result!.Items.Should().Contain(i => i.EventSummary == "Correct session event");
    }

    [Fact]
    public async Task GetAroundMessage_WrongSession_ReturnsNull()
    {
        // Arrange — message exists but belongs to a different session
        const string sessionA = "sess-msg-a";
        const string sessionB = "sess-msg-b";
        SeedSession(sessionA);
        SeedSession(sessionB);

        var messageTime = new DateTime(2026, 3, 28, 12, 1, 0, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-session-a", SessionId = sessionA,
                TimestampUtc = messageTime,
                Role = "User", Content = "Session A message"
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act — query with sessionB but message belongs to sessionA
        var result = await sut.GetAroundMessageAsync(sessionB, "msg-session-a", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().BeNull("the message belongs to session A, not session B");
    }

    [Fact]
    public async Task GetAroundMessage_CorrectSession_ReturnsContext()
    {
        // Arrange
        const string sessionId = "sess-correct-msg";
        SeedSession(sessionId);

        var messageTime = new DateTime(2026, 3, 28, 12, 1, 0, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-correct", SessionId = sessionId,
                TimestampUtc = messageTime,
                Role = "User", Content = "Correct session message"
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var retrieval = new ReplayRetrievalService(_dbPath, mockObs);
        var sut = new ReplayAnchorService(_dbPath, retrieval);

        // Act
        var result = await sut.GetAroundMessageAsync(sessionId, "msg-correct", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().NotBeNull("the message belongs to the requested session");
        result!.Items.Should().Contain(i => i.MessageContent == "Correct session message");
    }

    /// <summary>
    /// Fake observation store for anchor service tests (no observation data needed).
    /// </summary>
    private sealed class FakeObservationStore : IObservationStore
    {
        public Task<ObservationRecord> StoreAsync(ObservationWriteRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ObservationRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ObservationRecord>> GetByTimeRangeAsync(
            string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ObservationRecord>>(Array.Empty<ObservationRecord>());
    }
}
