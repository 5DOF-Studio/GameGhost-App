using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Context;

public sealed class ObservationStoreTimeRangeTests : IDisposable
{
    private readonly string _rootDirectory;

    public ObservationStoreTimeRangeTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "gaimer-obs-timerange-tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task GetByTimeRangeAsync_ReturnsMatchingRecords()
    {
        var sut = new SqliteObservationStore(_rootDirectory);
        var baseTime = DateTime.UtcNow;

        // Store 3 observations at different times
        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "before",
            CapturedAtUtc = baseTime.AddSeconds(-30),
            SourceTarget = "test",
            ArtifactBytes = [0x01],
            SessionId = "sess-1"
        });
        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "in-range",
            CapturedAtUtc = baseTime,
            SourceTarget = "test",
            ArtifactBytes = [0x02],
            SessionId = "sess-1"
        });
        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "after",
            CapturedAtUtc = baseTime.AddSeconds(30),
            SourceTarget = "test",
            ArtifactBytes = [0x03],
            SessionId = "sess-1"
        });

        // Query for a window that includes only the middle observation
        var results = await sut.GetByTimeRangeAsync("sess-1", baseTime.AddSeconds(-5), baseTime.AddSeconds(5));

        results.Should().ContainSingle(r => r.Id == "in-range");
    }

    [Fact]
    public async Task GetByTimeRangeAsync_FiltersOnSessionId()
    {
        var sut = new SqliteObservationStore(_rootDirectory);
        var baseTime = DateTime.UtcNow;

        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "sess1-obs",
            CapturedAtUtc = baseTime,
            SourceTarget = "test",
            ArtifactBytes = [0x01],
            SessionId = "sess-1"
        });
        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "sess2-obs",
            CapturedAtUtc = baseTime,
            SourceTarget = "test",
            ArtifactBytes = [0x02],
            SessionId = "sess-2"
        });

        var results = await sut.GetByTimeRangeAsync("sess-1", baseTime.AddSeconds(-5), baseTime.AddSeconds(5));

        results.Should().ContainSingle(r => r.Id == "sess1-obs");
    }

    [Fact]
    public async Task GetByTimeRangeAsync_ReturnsChronologicalOrder()
    {
        var sut = new SqliteObservationStore(_rootDirectory);
        var baseTime = DateTime.UtcNow;

        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "late",
            CapturedAtUtc = baseTime.AddSeconds(2),
            SourceTarget = "test",
            ArtifactBytes = [0x02],
            SessionId = "sess-1"
        });
        await sut.StoreAsync(new ObservationWriteRequest
        {
            Id = "early",
            CapturedAtUtc = baseTime,
            SourceTarget = "test",
            ArtifactBytes = [0x01],
            SessionId = "sess-1"
        });

        var results = await sut.GetByTimeRangeAsync("sess-1", baseTime.AddSeconds(-5), baseTime.AddSeconds(10));

        results.Should().HaveCount(2);
        results[0].Id.Should().Be("early");
        results[1].Id.Should().Be("late");
    }

    [Fact]
    public async Task GetByTimeRangeAsync_EmptyResult_ReturnsEmptyList()
    {
        var sut = new SqliteObservationStore(_rootDirectory);
        var baseTime = DateTime.UtcNow;

        var results = await sut.GetByTimeRangeAsync("nonexistent-sess", baseTime.AddSeconds(-5), baseTime.AddSeconds(5));

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }
        catch { }
    }
}
