using Microsoft.EntityFrameworkCore;
using WitnessDesktop.Data;
using WitnessDesktop.Data.Entities;

namespace WitnessDesktop.Tests.Data;

public class GaimerHistoryDbContextTests : IDisposable
{
    private readonly string _dbPath;

    public GaimerHistoryDbContextTests()
    {
        _dbPath = Path.GetTempFileName() + ".db";
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-journal"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
    }

    private GaimerHistoryDbContext CreateContext() =>
        GaimerHistoryDbContext.CreateForPath(_dbPath);

    [Fact]
    public void EnsureCreated_CreatesDatabase()
    {
        // Arrange & Act
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        // Insert a session and read it back
        var session = new SessionRecord
        {
            SessionId = "abc123def456",
            StartedAtUtc = DateTime.UtcNow,
            AgentKey = "chess",
            GameType = "chess",
            ConnectorName = "lichess"
        };
        ctx.Sessions.Add(session);
        ctx.SaveChanges();

        // Assert — read back in a fresh context
        using var ctx2 = CreateContext();
        var loaded = ctx2.Sessions.Find("abc123def456");
        loaded.Should().NotBeNull();
        loaded!.AgentKey.Should().Be("chess");
        loaded.GameType.Should().Be("chess");
        loaded.ConnectorName.Should().Be("lichess");
    }

    [Fact]
    public void SessionRecord_Cascades_DeleteChildren()
    {
        // Arrange
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        var session = new SessionRecord
        {
            SessionId = "sess001",
            StartedAtUtc = DateTime.UtcNow
        };

        var chatMsg = new ChatMessageRecord
        {
            Id = "msg001",
            SessionId = "sess001",
            TimestampUtc = DateTime.UtcNow,
            Role = "User",
            Content = "Hello"
        };

        var checkpoint = new TimelineCheckpointRecord
        {
            Id = "cp001",
            SessionId = "sess001",
            CreatedAtUtc = DateTime.UtcNow,
            DisplayOrder = 0
        };

        var timelineEvent = new TimelineEventRecord
        {
            Id = "evt001",
            SessionId = "sess001",
            CheckpointId = "cp001",
            CreatedAtUtc = DateTime.UtcNow,
            Type = "Assessment",
            Summary = "Position looks solid",
            DisplayOrder = 0
        };

        ctx.Sessions.Add(session);
        ctx.ChatMessages.Add(chatMsg);
        ctx.TimelineCheckpoints.Add(checkpoint);
        ctx.TimelineEvents.Add(timelineEvent);
        ctx.SaveChanges();

        // Act — delete the session
        using var ctx2 = CreateContext();
        var toDelete = ctx2.Sessions.Find("sess001");
        toDelete.Should().NotBeNull();
        ctx2.Sessions.Remove(toDelete!);
        ctx2.SaveChanges();

        // Assert — children are cascade-deleted
        using var ctx3 = CreateContext();
        ctx3.ChatMessages.Find("msg001").Should().BeNull();
        ctx3.TimelineCheckpoints.Find("cp001").Should().BeNull();
        ctx3.TimelineEvents.Find("evt001").Should().BeNull();
    }

    [Fact]
    public void ChatMessageRecord_RoundTrips_AllFields()
    {
        // Arrange
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        var session = new SessionRecord
        {
            SessionId = "sess002",
            StartedAtUtc = DateTime.UtcNow
        };

        var now = DateTime.UtcNow;
        var msg = new ChatMessageRecord
        {
            Id = "msg002",
            SessionId = "sess002",
            TimestampUtc = now,
            Role = "Assistant",
            Intent = "LiveGameInfo",
            Content = "You should play Nf3 to develop your knight.",
            Source = "brain",
            DeliveryState = "Sent",
            CorrelationId = "corr-xyz"
        };

        ctx.Sessions.Add(session);
        ctx.ChatMessages.Add(msg);
        ctx.SaveChanges();

        // Act — read back in fresh context
        using var ctx2 = CreateContext();
        var loaded = ctx2.ChatMessages.Find("msg002");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SessionId.Should().Be("sess002");
        loaded.TimestampUtc.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        loaded.Role.Should().Be("Assistant");
        loaded.Intent.Should().Be("LiveGameInfo");
        loaded.Content.Should().Be("You should play Nf3 to develop your knight.");
        loaded.Source.Should().Be("brain");
        loaded.DeliveryState.Should().Be("Sent");
        loaded.CorrelationId.Should().Be("corr-xyz");
    }

    [Fact]
    public void TimelineEventRecord_OptionalCheckpoint_Nullable()
    {
        // Arrange
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        var session = new SessionRecord
        {
            SessionId = "sess003",
            StartedAtUtc = DateTime.UtcNow
        };

        var evt = new TimelineEventRecord
        {
            Id = "evt002",
            SessionId = "sess003",
            CheckpointId = null, // No checkpoint — e.g. direct message
            CreatedAtUtc = DateTime.UtcNow,
            Type = "DirectMessage",
            Summary = "User asked about opening theory",
            FullContent = "What is the best opening for white?",
            DisplayOrder = 0
        };

        ctx.Sessions.Add(session);
        ctx.TimelineEvents.Add(evt);
        ctx.SaveChanges();

        // Act — read back
        using var ctx2 = CreateContext();
        var loaded = ctx2.TimelineEvents.Find("evt002");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.CheckpointId.Should().BeNull();
        loaded.Checkpoint.Should().BeNull();
        loaded.Type.Should().Be("DirectMessage");
        loaded.Summary.Should().Be("User asked about opening theory");
    }

    [Fact]
    public void CreateForPath_EnablesWalMode()
    {
        // Arrange — create DB with schema so the file exists
        using (var bootstrap = CreateContext())
        {
            bootstrap.Database.EnsureCreated();
        }

        // Act — open a fresh context (CreateForPath sets WAL + busy_timeout)
        using var ctx = CreateContext();

        // Assert — query the journal_mode pragma
        var conn = ctx.Database.GetDbConnection();
        // Connection is already open from CreateForPath
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var journalMode = cmd.ExecuteScalar()?.ToString();
        journalMode.Should().Be("wal");
    }

    [Fact]
    public void CreateForPath_SetsBusyTimeout()
    {
        // Arrange
        using (var bootstrap = CreateContext())
        {
            bootstrap.Database.EnsureCreated();
        }

        // Act
        using var ctx = CreateContext();

        // Assert
        var conn = ctx.Database.GetDbConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout;";
        var timeout = cmd.ExecuteScalar();
        Convert.ToInt32(timeout).Should().Be(5000);
    }
}
