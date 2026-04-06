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
    /// Delete ephemeral session files. Call AFTER orchestrator has drained
    /// to avoid deleting video files before analysis completes.
    /// </summary>
    void CleanupSessionFiles();
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
    public IReadOnlyList<ReplaySegment> GetAvailableSegments() => Array.Empty<ReplaySegment>();
#pragma warning disable CS0067
    public event EventHandler<ReplaySegmentCompletedEventArgs>? SegmentCompleted;
#pragma warning restore CS0067
}
