using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Context;

public sealed class SqliteObservationStoreTests : IDisposable
{
    private readonly string _rootDirectory;

    public SqliteObservationStoreTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "gaimer-observation-store-tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task StoreAsync_PersistsArtifactAndMetadata()
    {
        var sut = new SqliteObservationStore(_rootDirectory);
        var capturedAt = DateTime.UtcNow;

        var record = await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "frame-001",
            CapturedAtUtc = capturedAt,
            SourceTarget = "Chess|Game 1",
            ArtifactBytes = [1, 2, 3, 4],
            AgentKey = "leroy",
            SessionId = "session-1"
        });

        File.Exists(record.ArtifactPath).Should().BeTrue();
        record.ByteSize.Should().Be(4);

        var recent = await sut.GetRecentAsync(10);
        recent.Should().ContainSingle(r => r.Id == "frame-001" && r.SourceTarget == "Chess|Game 1");
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var sut = new SqliteObservationStore(_rootDirectory);

        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "old",
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-10),
            SourceTarget = "test",
            ArtifactBytes = [0x01]
        });
        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "new",
            CapturedAtUtc = DateTime.UtcNow,
            SourceTarget = "test",
            ArtifactBytes = [0x02]
        });

        var recent = await sut.GetRecentAsync(10);
        recent[0].Id.Should().Be("new");
        recent[1].Id.Should().Be("old");
    }

    [Fact]
    public async Task StoreAsync_TrimByAge_RemovesOldRowsAndArtifacts()
    {
        var sut = new SqliteObservationStore(_rootDirectory)
        {
            MaxAge = TimeSpan.FromSeconds(1),
            MaxCount = 50
        };

        var old = await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "old",
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-5),
            SourceTarget = "test",
            ArtifactBytes = [0x01]
        });

        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "new",
            CapturedAtUtc = DateTime.UtcNow,
            SourceTarget = "test",
            ArtifactBytes = [0x02]
        });

        var recent = await sut.GetRecentAsync(10);
        recent.Should().ContainSingle(r => r.Id == "new");
        File.Exists(old.ArtifactPath).Should().BeFalse();
    }

    [Fact]
    public async Task StoreAsync_TrimByCount_KeepsNewestArtifacts()
    {
        var sut = new SqliteObservationStore(_rootDirectory)
        {
            MaxAge = TimeSpan.FromHours(1),
            MaxCount = 2
        };

        var first = await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "first",
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-3),
            SourceTarget = "test",
            ArtifactBytes = [0x01]
        });

        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "second",
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-2),
            SourceTarget = "test",
            ArtifactBytes = [0x02]
        });

        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "third",
            CapturedAtUtc = DateTime.UtcNow.AddSeconds(-1),
            SourceTarget = "test",
            ArtifactBytes = [0x03]
        });

        var recent = await sut.GetRecentAsync(10);
        recent.Select(r => r.Id).Should().Equal("third", "second");
        File.Exists(first.ArtifactPath).Should().BeFalse();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temp test data.
        }
    }
}
