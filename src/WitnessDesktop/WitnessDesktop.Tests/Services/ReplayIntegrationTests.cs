using WitnessDesktop.Data;
using WitnessDesktop.Data.Entities;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.Services.History;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Services;

public sealed class ReplayIntegrationTests : IDisposable
{
    private readonly string _dbPath;

    public ReplayIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "gaimer-integration-tests", Guid.NewGuid().ToString("N") + ".db");
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

    [Fact]
    public async Task WriteAndRead_SessionRoundTrip()
    {
        // Arrange
        var historyService = new SessionHistoryService(_dbPath);
        var mockObs = new FakeObservationStore();
        var retrievalService = new ReplayRetrievalService(_dbPath, mockObs);

        const string sessionId = "sess-roundtrip";
        var baseTime = DateTime.UtcNow;

        // Act -- write session, chat messages, and timeline events
        await historyService.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-rt-1",
            Role = MessageRole.User,
            Content = "What should I do?",
            Timestamp = baseTime.AddSeconds(-4)
        });

        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-rt-2",
            Role = MessageRole.Assistant,
            Content = "Consider Nf3.",
            Timestamp = baseTime.AddSeconds(-3)
        });

        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-rt-3",
            Role = MessageRole.User,
            Content = "Good idea.",
            Timestamp = baseTime.AddSeconds(-1)
        });

        await historyService.PersistTimelineEventAsync(sessionId,
            new TimelineEvent
            {
                Id = "evt-rt-1",
                Timestamp = baseTime.AddSeconds(-2),
                Type = EventOutputType.Assessment,
                Summary = "Position is equal"
            }, null, 0);

        await historyService.PersistTimelineEventAsync(sessionId,
            new TimelineEvent
            {
                Id = "evt-rt-2",
                Timestamp = baseTime,
                Type = EventOutputType.SageAdvice,
                Summary = "Develop your pieces"
            }, null, 1);

        // Read back via retrieval service
        var result = await retrievalService.GetRecentAsync(sessionId, TimeSpan.FromMinutes(1));

        // Assert -- all 5 items present in chronological order
        result.Items.Should().HaveCount(5);

        // Verify chronological ordering
        for (int i = 1; i < result.Items.Count; i++)
        {
            result.Items[i].TimestampUtc.Should().BeOnOrAfter(result.Items[i - 1].TimestampUtc);
        }

        // Verify content round-trips
        result.Items.Should().Contain(i => i.Kind == ReplayItemKind.ChatMessage && i.MessageContent == "What should I do?");
        result.Items.Should().Contain(i => i.Kind == ReplayItemKind.ChatMessage && i.MessageContent == "Consider Nf3.");
        result.Items.Should().Contain(i => i.Kind == ReplayItemKind.ChatMessage && i.MessageContent == "Good idea.");
        result.Items.Should().Contain(i => i.Kind == ReplayItemKind.TimelineEvent && i.EventSummary == "Position is equal");
        result.Items.Should().Contain(i => i.Kind == ReplayItemKind.TimelineEvent && i.EventSummary == "Develop your pieces");
    }

    [Fact]
    public async Task WriteAndRead_AnchorRetrieval()
    {
        // Arrange
        var historyService = new SessionHistoryService(_dbPath);
        var mockObs = new FakeObservationStore();
        var retrievalService = new ReplayRetrievalService(_dbPath, mockObs);
        var anchorService = new ReplayAnchorService(_dbPath, retrievalService);

        const string sessionId = "sess-anchor-int";
        var baseTime = new DateTime(2026, 3, 28, 12, 0, 30, DateTimeKind.Utc);

        await historyService.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Anchor event at T+30s
        await historyService.PersistTimelineEventAsync(sessionId,
            new TimelineEvent
            {
                Id = "evt-anchor-target",
                Timestamp = baseTime,
                Type = EventOutputType.Danger,
                Summary = "Knight fork detected"
            }, null, 0);

        // Nearby message at T+25s (within 10s radius)
        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-anchor-near",
            Role = MessageRole.User,
            Content = "Is there a threat?",
            Timestamp = baseTime.AddSeconds(-5)
        });

        // Far message at T+0 (outside 10s radius)
        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-anchor-far",
            Role = MessageRole.User,
            Content = "Starting game",
            Timestamp = baseTime.AddSeconds(-20)
        });

        // Act -- retrieve around the anchor event with 10s radius
        var result = await anchorService.GetAroundEventAsync(sessionId, "evt-anchor-target", TimeSpan.FromSeconds(10));

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().Contain(i => i.EventSummary == "Knight fork detected");
        result.Items.Should().Contain(i => i.MessageContent == "Is there a threat?");
        result.Items.Should().NotContain(i => i.MessageContent == "Starting game");
    }

    [Fact]
    public async Task BoundedRetention_5MinuteClamp()
    {
        // Arrange
        var historyService = new SessionHistoryService(_dbPath);
        var mockObs = new FakeObservationStore();
        var retrievalService = new ReplayRetrievalService(_dbPath, mockObs);

        const string sessionId = "sess-clamp-int";
        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        await historyService.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Message at T+9min (will be within clamped 5-min window)
        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-recent-int",
            Role = MessageRole.User,
            Content = "Recent event",
            Timestamp = baseTime.AddMinutes(9)
        });

        // Message at T+0 (will be outside clamped 5-min window)
        await historyService.PersistChatMessageAsync(sessionId, new ChatMessage
        {
            Id = "msg-old-int",
            Role = MessageRole.User,
            Content = "Old event",
            Timestamp = baseTime
        });

        // Act -- request 10-minute window, should be clamped to last 5 minutes
        var result = await retrievalService.GetByTimeWindowAsync(
            sessionId, baseTime, baseTime.AddMinutes(10));

        // Assert -- only the message within the clamped window (T+5 to T+10)
        result.Items.Should().ContainSingle(i => i.MessageContent == "Recent event");
        result.Items.Should().NotContain(i => i.MessageContent == "Old event");
    }

    [Fact]
    public async Task MissingArtifact_GracefullySkipped()
    {
        // Arrange -- observation store returns records with nonexistent file paths
        var mockObs = new FakeObservationStore(new[]
        {
            new ObservationRecord
            {
                Id = "obs-missing-int",
                CapturedAtUtc = DateTime.UtcNow.AddSeconds(-5),
                ArtifactPath = "/nonexistent/path/that/does/not/exist.jpg",
                SourceTarget = "test",
                SessionId = "sess-missing-int",
                ByteSize = 100
            }
        });

        // Seed session in DB
        using (var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath))
        {
            ctx.Database.EnsureCreated();
            ctx.Sessions.Add(new SessionRecord
            {
                SessionId = "sess-missing-int",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            });
            ctx.SaveChanges();
        }

        var retrievalService = new ReplayRetrievalService(_dbPath, mockObs);

        // Act -- should NOT throw
        var result = await retrievalService.GetRecentAsync("sess-missing-int", TimeSpan.FromMinutes(1));

        // Assert -- item present with ArtifactExists = false
        result.Items.Should().ContainSingle();
        result.Items[0].ArtifactExists.Should().BeFalse();
        result.Items[0].Kind.Should().Be(ReplayItemKind.CaptureArtifact);
    }

    [Fact]
    public async Task PersistenceFailure_DoesNotCrash()
    {
        // Arrange -- create a valid service, then corrupt the DB
        var historyService = new SessionHistoryService(_dbPath);
        await historyService.StartSessionAsync("sess-corrupt", "leroy", "chess", null, null, null);

        // Corrupt the DB by renaming the original and writing garbage
        var corruptPath = _dbPath + ".backup";
        File.Copy(_dbPath, corruptPath, overwrite: true);

        // Overwrite original with garbage so SQLite cannot read it
        await File.WriteAllTextAsync(_dbPath, "THIS IS NOT A SQLITE DATABASE FILE");

        // Act -- persist should NOT throw (fire-and-forget safe)
        Func<Task> act = async () => await historyService.PersistChatMessageAsync("sess-corrupt", new ChatMessage
        {
            Id = "msg-fail",
            Role = MessageRole.User,
            Content = "This should not crash",
            Timestamp = DateTime.UtcNow
        });

        // Assert -- no exception thrown
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EmptySession_ReturnsEmptyReplay()
    {
        // Arrange
        var historyService = new SessionHistoryService(_dbPath);
        var mockObs = new FakeObservationStore();
        var retrievalService = new ReplayRetrievalService(_dbPath, mockObs);

        const string sessionId = "sess-empty-int";
        await historyService.StartSessionAsync(sessionId, "leroy", "chess", null, null, null);

        // Act -- retrieve without persisting any data
        var result = await retrievalService.GetRecentAsync(sessionId, TimeSpan.FromMinutes(1));

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.SessionId.Should().Be(sessionId);
    }

    /// <summary>
    /// Fake observation store for integration tests.
    /// </summary>
    private sealed class FakeObservationStore : IObservationStore
    {
        private readonly IReadOnlyList<ObservationRecord> _records;

        public FakeObservationStore(IEnumerable<ObservationRecord>? records = null)
        {
            _records = records?.ToList() ?? [];
        }

        public Task<ObservationRecord> StoreAsync(ObservationWriteRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ObservationRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ObservationRecord>> GetByTimeRangeAsync(
            string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
        {
            var filtered = _records
                .Where(r => r.SessionId == sessionId && r.CapturedAtUtc >= startUtc && r.CapturedAtUtc <= endUtc)
                .OrderBy(r => r.CapturedAtUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<ObservationRecord>>(filtered);
        }
    }
}
