using WitnessDesktop.Data;
using WitnessDesktop.Data.Entities;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Services;

public sealed class ReplayRetrievalServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _obsRootDir;

    public ReplayRetrievalServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "gaimer-replay-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _obsRootDir = Path.Combine(Path.GetTempPath(), "gaimer-replay-obs-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        foreach (var ext in new[] { "", "-journal", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + ext); } catch { }
        }
        try { Directory.Delete(Path.GetDirectoryName(_dbPath)!, true); } catch { }
        try { Directory.Delete(_obsRootDir, true); } catch { }
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
    public async Task GetByTimeWindow_ReturnsMessagesAndEvents_InChronologicalOrder()
    {
        // Arrange
        const string sessionId = "sess-chrono";
        SeedSession(sessionId);

        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            ctx.ChatMessages.AddRange(
                new ChatMessageRecord { Id = "msg1", SessionId = sessionId, TimestampUtc = baseTime.AddSeconds(1), Role = "User", Content = "Hello" },
                new ChatMessageRecord { Id = "msg2", SessionId = sessionId, TimestampUtc = baseTime.AddSeconds(3), Role = "Assistant", Content = "Hi there" },
                new ChatMessageRecord { Id = "msg3", SessionId = sessionId, TimestampUtc = baseTime.AddSeconds(5), Role = "User", Content = "What move?" }
            );
            ctx.TimelineEvents.AddRange(
                new TimelineEventRecord { Id = "evt1", SessionId = sessionId, CreatedAtUtc = baseTime.AddSeconds(2), Type = "Assessment", Summary = "Position solid", DisplayOrder = 0 },
                new TimelineEventRecord { Id = "evt2", SessionId = sessionId, CreatedAtUtc = baseTime.AddSeconds(4), Type = "Danger", Summary = "Threat on d5", DisplayOrder = 1 }
            );
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var sut = new ReplayRetrievalService(_dbPath, mockObs);

        // Act
        var result = await sut.GetByTimeWindowAsync(sessionId, baseTime, baseTime.AddSeconds(10));

        // Assert -- 5 items total, in chronological order
        result.Items.Should().HaveCount(5);
        result.Items[0].Kind.Should().Be(ReplayItemKind.ChatMessage);
        result.Items[0].MessageContent.Should().Be("Hello");
        result.Items[1].Kind.Should().Be(ReplayItemKind.TimelineEvent);
        result.Items[1].EventSummary.Should().Be("Position solid");
        result.Items[2].Kind.Should().Be(ReplayItemKind.ChatMessage);
        result.Items[2].MessageContent.Should().Be("Hi there");
        result.Items[3].Kind.Should().Be(ReplayItemKind.TimelineEvent);
        result.Items[3].EventSummary.Should().Be("Threat on d5");
        result.Items[4].Kind.Should().Be(ReplayItemKind.ChatMessage);
        result.Items[4].MessageContent.Should().Be("What move?");
    }

    [Fact]
    public async Task GetByTimeWindow_ClampsTo5Minutes()
    {
        // Arrange
        const string sessionId = "sess-clamp";
        SeedSession(sessionId);

        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        using (var ctx = CreateContext())
        {
            // Message at T+1min (within 5-min clamp from end)
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-in", SessionId = sessionId, TimestampUtc = baseTime.AddMinutes(9),
                Role = "User", Content = "Recent"
            });
            // Message at T+0 (outside 5-min clamp from end)
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-out", SessionId = sessionId, TimestampUtc = baseTime,
                Role = "User", Content = "Old"
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var sut = new ReplayRetrievalService(_dbPath, mockObs);

        // Act -- request 10-minute window, should be clamped to last 5 minutes
        var result = await sut.GetByTimeWindowAsync(sessionId, baseTime, baseTime.AddMinutes(10));

        // Assert -- only the message within the clamped 5-min window (T+5 to T+10)
        result.Items.Should().ContainSingle(i => i.MessageContent == "Recent");
        result.Items.Should().NotContain(i => i.MessageContent == "Old");
    }

    [Fact]
    public async Task GetByTimeWindow_IncludesObservationArtifacts()
    {
        // Arrange
        const string sessionId = "sess-obs";
        SeedSession(sessionId);

        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        // Create actual artifact files
        var artifactDir = Path.Combine(_obsRootDir, "frames");
        Directory.CreateDirectory(artifactDir);
        var artifactPath1 = Path.Combine(artifactDir, "frame1.jpg");
        var artifactPath2 = Path.Combine(artifactDir, "frame2.jpg");
        await File.WriteAllBytesAsync(artifactPath1, [0x01, 0x02]);
        await File.WriteAllBytesAsync(artifactPath2, [0x03, 0x04]);

        var mockObs = new FakeObservationStore(new[]
        {
            new ObservationRecord
            {
                Id = "obs1", CapturedAtUtc = baseTime.AddSeconds(1),
                ArtifactPath = artifactPath1, SourceTarget = "test",
                SessionId = sessionId, ByteSize = 2
            },
            new ObservationRecord
            {
                Id = "obs2", CapturedAtUtc = baseTime.AddSeconds(3),
                ArtifactPath = artifactPath2, SourceTarget = "test",
                SessionId = sessionId, ByteSize = 2
            }
        });

        var sut = new ReplayRetrievalService(_dbPath, mockObs);

        // Act
        var result = await sut.GetByTimeWindowAsync(sessionId, baseTime, baseTime.AddSeconds(10));

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Kind == ReplayItemKind.CaptureArtifact);
        result.Items.Should().OnlyContain(i => i.ArtifactExists);
    }

    [Fact]
    public async Task GetByTimeWindow_MissingArtifact_SetsArtifactExistsFalse()
    {
        // Arrange
        const string sessionId = "sess-missing";
        SeedSession(sessionId);

        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        var mockObs = new FakeObservationStore(new[]
        {
            new ObservationRecord
            {
                Id = "obs-gone", CapturedAtUtc = baseTime.AddSeconds(1),
                ArtifactPath = "/nonexistent/path/frame.jpg", SourceTarget = "test",
                SessionId = sessionId, ByteSize = 100
            }
        });

        var sut = new ReplayRetrievalService(_dbPath, mockObs);

        // Act
        var result = await sut.GetByTimeWindowAsync(sessionId, baseTime, baseTime.AddSeconds(10));

        // Assert
        result.Items.Should().ContainSingle();
        result.Items[0].ArtifactExists.Should().BeFalse();
        result.Items[0].ArtifactPath.Should().Be("/nonexistent/path/frame.jpg");
    }

    [Fact]
    public async Task GetRecentAsync_DelegatesToTimeWindow()
    {
        // Arrange
        const string sessionId = "sess-recent";
        SeedSession(sessionId);

        var now = DateTime.UtcNow;

        using (var ctx = CreateContext())
        {
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-recent", SessionId = sessionId,
                TimestampUtc = now.AddSeconds(-10),
                Role = "User", Content = "Recent message"
            });
            // Old message outside the 30s window
            ctx.ChatMessages.Add(new ChatMessageRecord
            {
                Id = "msg-old", SessionId = sessionId,
                TimestampUtc = now.AddMinutes(-2),
                Role = "User", Content = "Old message"
            });
            ctx.SaveChanges();
        }

        var mockObs = new FakeObservationStore();
        var sut = new ReplayRetrievalService(_dbPath, mockObs);

        // Act
        var result = await sut.GetRecentAsync(sessionId, TimeSpan.FromSeconds(30));

        // Assert -- only the recent message within 30s
        result.Items.Should().ContainSingle(i => i.MessageContent == "Recent message");
        result.Items.Should().NotContain(i => i.MessageContent == "Old message");
    }

    [Fact]
    public async Task GetByTimeWindow_EmptyResult_ReturnsEmptyItems()
    {
        // Arrange
        const string sessionId = "sess-empty";
        SeedSession(sessionId);

        var baseTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);
        var mockObs = new FakeObservationStore();
        var sut = new ReplayRetrievalService(_dbPath, mockObs);

        // Act -- query a time range with no data
        var result = await sut.GetByTimeWindowAsync(sessionId, baseTime, baseTime.AddSeconds(10));

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.SessionId.Should().Be(sessionId);
    }

    /// <summary>
    /// Fake observation store that returns preconfigured records for GetByTimeRangeAsync.
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
