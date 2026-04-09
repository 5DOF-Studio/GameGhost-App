using System.IO;

namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Manages continuous screen recording with 2:30 segment rotation.
/// Keeps at most 2 completed segments on disk (rolling buffer).
/// </summary>
internal sealed class ReplayRecordingService : IReplayRecordingService, IDisposable
{
    private static readonly TimeSpan SegmentDuration = TimeSpan.FromSeconds(150); // 2:30
    private const int MaxCompletedSegments = 2;
    private const int DefaultRecordingWidth = 1920;
    private const int DefaultRecordingHeight = 1080;
    private const string RecentSubdir = "recent";
    private static readonly TimeSpan StaleReplayThreshold = TimeSpan.FromHours(24);

    private readonly INativeRecordingBridge _bridge;
    private readonly string _baseDir;
    private readonly ISessionTraceService? _trace;
    private readonly long _minimumDiskSpaceBytes;
    private readonly object _lock = new();
    private readonly List<ReplaySegment> _completedSegments = new();

    private PeriodicTimer? _rotationTimer;
    private CancellationTokenSource? _rotationCts;
    private Task? _rotationLoop;
    private string? _currentSessionId;
    private string? _currentSessionDir;
    private int _currentSegmentIndex;
    private DateTimeOffset _currentSegmentStartUtc;
    private bool _disposed;

    public bool IsRecording { get; private set; }

    public event EventHandler<ReplaySegmentCompletedEventArgs>? SegmentCompleted;

    public ReplayRecordingService(
        INativeRecordingBridge bridge,
        string baseDir,
        ISessionTraceService? trace = null,
        int minimumDiskSpaceMb = 500)
    {
        _bridge = bridge;
        _baseDir = baseDir;
        _trace = trace;
        _minimumDiskSpaceBytes = (long)minimumDiskSpaceMb * 1024 * 1024;
    }

    public async Task StartAsync(uint windowId, string sessionId, CancellationToken ct = default)
    {
        if (IsRecording) return;

        if (!HasSufficientDiskSpace())
        {
            _trace?.TrackEvent("replay.recording.skipped", new Dictionary<string, string>
            {
                ["reason"] = "insufficient_disk_space"
            });
            return;
        }

        // W2: Clear stale segments from prior session
        lock (_lock) { _completedSegments.Clear(); }

        _currentSessionId = sessionId;
        _currentSessionDir = Path.Combine(_baseDir, sessionId);
        Directory.CreateDirectory(_currentSessionDir);

        _currentSegmentIndex = 0;
        _currentSegmentStartUtc = DateTimeOffset.UtcNow;
        var outputPath = GetSegmentPath(_currentSegmentIndex);

        var started = _bridge.StartRecording(windowId, outputPath, DefaultRecordingWidth, DefaultRecordingHeight);
        if (!started)
        {
            _trace?.TrackError("Native recording failed to start", "ReplayRecordingService");
            return;
        }

        // C1: sck_start_recording returns true immediately but native setup is async.
        // Poll status to confirm recording actually started before setting IsRecording.
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(200, ct);
            if (_bridge.GetStatus() == 1) break; // 1 = recording
        }

        if (_bridge.GetStatus() != 1)
        {
            _trace?.TrackError("Native recording failed to confirm start within 2s", "ReplayRecordingService");
            return;
        }

        IsRecording = true;

        _trace?.TrackEvent("replay.recording.started", new Dictionary<string, string>
        {
            ["session_id"] = sessionId,
            ["window_id"] = windowId.ToString(),
            ["segment_duration_s"] = SegmentDuration.TotalSeconds.ToString()
        });

        _rotationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _rotationTimer = new PeriodicTimer(SegmentDuration);
        _rotationLoop = RunRotationLoopAsync(_rotationCts.Token);
    }

    public async Task StopAsync()
    {
        if (!IsRecording) return;

        // Cancel rotation timer
        _rotationCts?.Cancel();
        _rotationTimer?.Dispose();
        _rotationTimer = null;

        // C2: Stop native BEFORE awaiting rotation loop. Native stop fires
        // cleanupState which calls pending rotationCompletion callback, unblocking
        // any in-flight RotateSegmentAsync. Without this, StopAsync hangs if
        // rotation is waiting for a frame that never arrives.
        await _bridge.StopRecordingAsync();

        // Now rotation loop should be unblocked — wait with 5s safety timeout
        if (_rotationLoop != null)
        {
            try
            {
                await Task.WhenAny(_rotationLoop, Task.Delay(5000));
            }
            catch (OperationCanceledException) { }
            _rotationLoop = null;
        }

        FinalizeCurrentSegment();

        // W3: Prune after finalizing stop segment (keeps max 2)
        PruneOldSegments();

        IsRecording = false;

        // W4: Dispose CTS to avoid per-session resource leak
        _rotationCts?.Dispose();
        _rotationCts = null;

        var totalSegments = _completedSegments.Count.ToString();

        _trace?.TrackEvent("replay.recording.stopped", new Dictionary<string, string>
        {
            ["session_id"] = _currentSessionId ?? "unknown",
            ["total_segments"] = totalSegments
        });
    }

    /// <inheritdoc/>
    public void CleanupSessionFiles()
    {
        if (_currentSessionDir == null || !Directory.Exists(_currentSessionDir))
        {
            lock (_lock) { _completedSegments.Clear(); }
            return;
        }

        var recentDir = Path.Combine(_baseDir, RecentSubdir);
        Directory.CreateDirectory(recentDir);

        try
        {
            // Move all .mp4 files from session dir to recent/
            foreach (var file in Directory.GetFiles(_currentSessionDir, "*.mp4"))
            {
                var destPath = Path.Combine(recentDir,
                    $"{_currentSessionId}_{Path.GetFileName(file)}");
                try
                {
                    File.Move(file, destPath, overwrite: true);
                }
                catch (IOException) { }
            }

            // Remove the now-empty session directory
            try { Directory.Delete(_currentSessionDir, recursive: true); }
            catch (IOException) { }
        }
        catch (IOException) { }

        lock (_lock) { _completedSegments.Clear(); }
    }

    /// <inheritdoc/>
    public void SweepStaleReplays()
    {
        var recentDir = Path.Combine(_baseDir, RecentSubdir);
        if (!Directory.Exists(recentDir))
            return;

        var cutoff = DateTime.UtcNow - StaleReplayThreshold;
        foreach (var file in Directory.GetFiles(recentDir))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch (IOException) { }
        }

        _trace?.TrackEvent("replay.sweep_stale_completed", new Dictionary<string, string>
        {
            ["recent_dir"] = recentDir
        });
    }

    /// <summary>
    /// Rotate to a new segment. Called by the rotation timer or externally for testing.
    /// </summary>
    public async Task RotateSegmentAsync()
    {
        if (!IsRecording) return;

        // W6: Recheck disk space before creating new segment.
        // Skip rotation if low — current segment keeps recording, next tick rechecks.
        if (!HasSufficientDiskSpace())
        {
            _trace?.TrackEvent("replay.recording.rotation_skipped", new Dictionary<string, string>
            {
                ["reason"] = "insufficient_disk_space"
            });
            return;
        }

        var nextIndex = _currentSegmentIndex + 1;
        var nextPath = GetSegmentPath(nextIndex);

        await _bridge.RotateSegmentAsync(nextPath);

        FinalizeCurrentSegment();

        _currentSegmentIndex = nextIndex;
        _currentSegmentStartUtc = DateTimeOffset.UtcNow;

        PruneOldSegments();
    }

    public IReadOnlyList<ReplaySegment> GetAvailableSegments()
    {
        lock (_lock)
        {
            return _completedSegments.ToList().AsReadOnly();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rotationCts?.Cancel();
        _rotationTimer?.Dispose();
        _rotationCts?.Dispose();
    }

    private async Task RunRotationLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _rotationTimer!.WaitForNextTickAsync(ct))
            {
                // C3: Check native health before rotation. If SCStream errored
                // (window closed, permission revoked), stop the managed loop.
                var nativeStatus = _bridge.GetStatus();
                if (nativeStatus != 1) // not recording
                {
                    _trace?.TrackEvent("replay.recording.native_error_detected", new Dictionary<string, string>
                    {
                        ["native_status"] = nativeStatus.ToString(),
                        ["session_id"] = _currentSessionId ?? "unknown"
                    });
                    IsRecording = false;
                    break;
                }

                try
                {
                    await RotateSegmentAsync();
                }
                catch (Exception ex)
                {
                    _trace?.TrackError($"Rotation failed: {ex.Message}", "ReplayRecordingService");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void FinalizeCurrentSegment()
    {
        if (_currentSessionDir == null || _currentSessionId == null) return;

        var segmentPath = GetSegmentPath(_currentSegmentIndex);
        if (!File.Exists(segmentPath)) return;

        var fileInfo = new FileInfo(segmentPath);
        var segment = new ReplaySegment
        {
            FilePath = segmentPath,
            SessionId = _currentSessionId,
            StartUtc = _currentSegmentStartUtc,
            EndUtc = DateTimeOffset.UtcNow,
            ByteSize = fileInfo.Length,
            SegmentIndex = _currentSegmentIndex
        };

        lock (_lock)
        {
            _completedSegments.Add(segment);
        }

        SegmentCompleted?.Invoke(this, new ReplaySegmentCompletedEventArgs { Segment = segment });
    }

    private void PruneOldSegments()
    {
        lock (_lock)
        {
            while (_completedSegments.Count > MaxCompletedSegments)
            {
                var oldest = _completedSegments[0];
                _completedSegments.RemoveAt(0);
                try
                {
                    if (File.Exists(oldest.FilePath))
                        File.Delete(oldest.FilePath);
                }
                catch (IOException) { }
            }
        }
    }

    private string GetSegmentPath(int index)
    {
        return Path.Combine(_currentSessionDir!, $"segment-{index}.mp4");
    }

    private bool HasSufficientDiskSpace()
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(_baseDir) ?? "/");
            return driveInfo.AvailableFreeSpace >= _minimumDiskSpaceBytes;
        }
        catch
        {
            return true;
        }
    }
}
