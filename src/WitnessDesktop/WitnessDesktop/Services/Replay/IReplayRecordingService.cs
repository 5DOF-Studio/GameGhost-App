namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Manages continuous screen recording with segment rotation.
/// Records gameplay to rolling 2:30 MP4 segments (keeps 2, deletes oldest).
/// </summary>
public interface IReplayRecordingService
{
    bool IsRecording { get; }
    Task StartAsync(uint windowId, string sessionId, CancellationToken ct = default);
    Task StopAsync();
    IReadOnlyList<ReplaySegment> GetAvailableSegments();
    event EventHandler<ReplaySegmentCompletedEventArgs>? SegmentCompleted;

    /// <summary>
    /// Move ephemeral session files to ~/Library/replays/recent/ for 24h retention.
    /// Call AFTER orchestrator has drained to avoid moving video files before analysis completes.
    /// </summary>
    void CleanupSessionFiles();

    /// <summary>
    /// Delete files in the recent replays staging directory that are older than 24 hours.
    /// Call on session start to keep disk usage bounded.
    /// </summary>
    void SweepStaleReplays();
}

/// <summary>
/// No-op implementation for platforms without native recording support (Windows).
/// </summary>
internal sealed class NullReplayRecordingService : IReplayRecordingService
{
    public bool IsRecording => false;
    public Task StartAsync(uint windowId, string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void CleanupSessionFiles() { }
    public void SweepStaleReplays() { }
    public IReadOnlyList<ReplaySegment> GetAvailableSegments() => Array.Empty<ReplaySegment>();
#pragma warning disable CS0067
    public event EventHandler<ReplaySegmentCompletedEventArgs>? SegmentCompleted;
#pragma warning restore CS0067
}
