using FluentAssertions;
using Moq;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Replay;
using Xunit;

namespace WitnessDesktop.Tests.Services;

public class ReplayCleanupPolicyTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _recentDir;
    private readonly Mock<INativeRecordingBridge> _mockBridge;
    private readonly ReplayRecordingService _sut;

    public ReplayCleanupPolicyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"gaimer-cleanup-test-{Guid.NewGuid():N}");
        _recentDir = Path.Combine(_testDir, "recent");
        Directory.CreateDirectory(_testDir);

        _mockBridge = new Mock<INativeRecordingBridge>();
        _mockBridge.Setup(b => b.StartRecording(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(true);
        _mockBridge.Setup(b => b.StopRecordingAsync()).Returns(Task.CompletedTask);
        _mockBridge.Setup(b => b.RotateSegmentAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockBridge.Setup(b => b.GetStatus()).Returns(1);

        _sut = new ReplayRecordingService(_mockBridge.Object, _testDir);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // ── CleanupSessionFiles moves to recent/ ────────────────────────────

    [Fact]
    public async Task CleanupSessionFiles_MovesSegmentsToRecentDir()
    {
        // Start a session so we have a session dir
        await _sut.StartAsync(12345, "session-1");
        var sessionDir = Path.Combine(_testDir, "session-1");

        // Create a fake segment file in the session dir
        var segmentPath = Path.Combine(sessionDir, "segment-0.mp4");
        await File.WriteAllBytesAsync(segmentPath, new byte[1024]);

        await _sut.StopAsync();
        _sut.CleanupSessionFiles();

        // Original session dir should be gone
        Directory.Exists(sessionDir).Should().BeFalse();

        // Files should exist in recent/
        Directory.Exists(_recentDir).Should().BeTrue();
        Directory.GetFiles(_recentDir, "*.mp4").Should().NotBeEmpty();
    }

    [Fact]
    public void CleanupSessionFiles_NoSessionDir_DoesNotThrow()
    {
        var act = () => _sut.CleanupSessionFiles();
        act.Should().NotThrow();
    }

    // ── SweepStaleReplays ───────────────────────────────────────────────

    [Fact]
    public void SweepStaleReplays_DeletesFilesOlderThan24Hours()
    {
        Directory.CreateDirectory(_recentDir);

        // Create stale file (set last write time to 25 hours ago)
        var stalePath = Path.Combine(_recentDir, "old-segment.mp4");
        File.WriteAllBytes(stalePath, new byte[512]);
        File.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddHours(-25));

        // Create fresh file (within 24 hours)
        var freshPath = Path.Combine(_recentDir, "fresh-segment.mp4");
        File.WriteAllBytes(freshPath, new byte[512]);

        _sut.SweepStaleReplays();

        File.Exists(stalePath).Should().BeFalse();
        File.Exists(freshPath).Should().BeTrue();
    }

    [Fact]
    public void SweepStaleReplays_NoRecentDir_DoesNotThrow()
    {
        // recent/ does not exist
        var act = () => _sut.SweepStaleReplays();
        act.Should().NotThrow();
    }

    [Fact]
    public void SweepStaleReplays_EmptyRecentDir_DoesNotThrow()
    {
        Directory.CreateDirectory(_recentDir);
        var act = () => _sut.SweepStaleReplays();
        act.Should().NotThrow();
    }

    [Fact]
    public void SweepStaleReplays_AllFilesFresh_DeletesNothing()
    {
        Directory.CreateDirectory(_recentDir);
        var freshPath = Path.Combine(_recentDir, "fresh-segment.mp4");
        File.WriteAllBytes(freshPath, new byte[512]);

        _sut.SweepStaleReplays();

        File.Exists(freshPath).Should().BeTrue();
    }
}
