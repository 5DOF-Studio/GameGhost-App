using System.Collections.ObjectModel;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services.History;

namespace WitnessDesktop.Services;

public class TimelineFeed : ITimelineFeed
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionTraceService? _sessionTrace;
    private readonly ISessionHistoryService? _historyService;
    private readonly TimeSpan _retentionWindow;
    private readonly int _sweepIntervalMs;
    private System.Timers.Timer? _sweepTimer;
    private TimelineEvent? _latestNonChatEvent;
    private int _checkpointDisplayOrder;

    public ObservableCollection<TimelineCheckpoint> Checkpoints { get; } = new();

    public TimelineCheckpoint? CurrentCheckpoint =>
        Checkpoints.FirstOrDefault(c => !c.IsArchiveBoundary);

    public event EventHandler<TimelineCheckpoint>? CheckpointCreated;

    public TimelineFeed(
        ISessionManager sessionManager,
        ISessionTraceService? sessionTrace = null,
        ISessionHistoryService? historyService = null,
        TimeSpan? retentionWindow = null,
        int sweepIntervalMs = 60_000)
    {
        _sessionManager = sessionManager;
        _sessionTrace = sessionTrace;
        _historyService = historyService;
        _retentionWindow = retentionWindow ?? TimeSpan.FromMinutes(5);
        _sweepIntervalMs = Math.Max(1000, sweepIntervalMs);
    }

    /// <summary>
    /// Records a capture silently. Does NOT create a visible checkpoint.
    /// Bucket creation is driven by AddEvent when brain output arrives.
    /// </summary>
    public void NewCapture(string screenshotRef, TimeSpan gameTime, string method)
    {
        // Captures are infrastructure — no visible timeline entry.
        // The brain will produce events that land in minute-based buckets.
    }

    public TimelineCheckpoint NewConversationCheckpoint()
    {
        var now = DateTime.UtcNow;
        var checkpoint = new TimelineCheckpoint
        {
            Context = SessionState.OutGame,
            Timestamp = now,
            BucketMinute = TruncateToMinute(now),
        };

        DispatchToMainThread(() =>
        {
            Checkpoints.Insert(0, checkpoint);
            CheckpointCreated?.Invoke(this, checkpoint);
        });

        PersistCheckpointIfActive(checkpoint);

        return checkpoint;
    }

    public void AddEvent(TimelineEvent evt)
    {
        _sessionTrace?.TrackEvent("timeline.event_emitted", new Dictionary<string, string>
        {
            ["output_type"] = evt.Type.ToString(),
            ["summary_length"] = (evt.Summary?.Length ?? 0).ToString()
        });

        DispatchToMainThread(() =>
        {
            var checkpoint = GetOrCreateBucket(evt.Timestamp);

            // DirectMessage: always expanded, never participates in compression
            if (evt.IsDirectChat)
            {
                evt.IsExpanded = true;
                // Each DirectMessage gets its own EventLine (newest at top)
                checkpoint.EventLines.Insert(0, new EventLine
                {
                    OutputType = evt.Type,
                    Events = new ObservableCollection<TimelineEvent> { evt },
                });
                return;
            }

            // Non-chat: always expanded in current bucket, one row per output
            evt.IsExpanded = true;
            evt.IsLatest = true;

            // Update global latest non-chat tracking
            if (_latestNonChatEvent != null)
            {
                _latestNonChatEvent.IsLatest = false;
            }
            _latestNonChatEvent = evt;

            // Current bucket: every event gets its own EventLine (no grouping)
            // Grouping only happens when the bucket ages out via CompressOlderBucket
            checkpoint.EventLines.Insert(0, new EventLine
            {
                OutputType = evt.Type,
                Events = new ObservableCollection<TimelineEvent> { evt },
            });
        });

        // Lazy timer: start sweep timer on first event added
        if (_sweepTimer == null)
        {
            _sweepTimer = new System.Timers.Timer(_sweepIntervalMs);
            _sweepTimer.Elapsed += (_, _) => EnforceRetention();
            _sweepTimer.AutoReset = true;
            _sweepTimer.Start();
        }
    }

    /// <summary>
    /// Inserts a grey "Archived" boundary checkpoint at the end of the timeline (bottom of scroll).
    /// This is a global end-of-scroll marker, not tied to any specific capture/conversation checkpoint.
    /// The retention engine will call this when establishing the hot window boundary.
    /// </summary>
    public void InsertArchiveBoundary()
    {
        DispatchToMainThread(() =>
        {
            // Remove any existing archive boundary first (only one at a time)
            for (int i = Checkpoints.Count - 1; i >= 0; i--)
            {
                if (Checkpoints[i].IsArchiveBoundary)
                {
                    Checkpoints.RemoveAt(i);
                    break;
                }
            }

            // Append at end = bottom of newest-first feed
            Checkpoints.Add(new TimelineCheckpoint
            {
                IsArchiveBoundary = true,
            });
        });
    }

    /// <summary>
    /// Prunes checkpoints older than the retention window from the timeline.
    /// The current checkpoint (newest non-archive bucket) is always exempt.
    /// When checkpoints are pruned, an archive boundary marker is inserted.
    /// Called by the sweep timer every <see cref="_sweepIntervalMs"/> milliseconds.
    /// </summary>
    internal void EnforceRetention()
    {
        DispatchToMainThread(() =>
        {
            var cutoff = DateTime.UtcNow - _retentionWindow;
            var current = CurrentCheckpoint;
            var removedAny = false;

            for (int i = Checkpoints.Count - 1; i >= 0; i--)
            {
                var cp = Checkpoints[i];

                // Never remove the archive boundary marker itself
                if (cp.IsArchiveBoundary) continue;

                // Current checkpoint is always exempt (even if old)
                if (cp == current) continue;

                if (cp.Timestamp < cutoff)
                {
                    // Check if _latestNonChatEvent lives in this checkpoint
                    if (_latestNonChatEvent != null)
                    {
                        foreach (var line in cp.EventLines)
                        {
                            if (line.Events.Contains(_latestNonChatEvent))
                            {
                                _latestNonChatEvent = null;
                                break;
                            }
                        }
                    }

                    Checkpoints.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                InsertArchiveBoundary();
            }
        });
    }

    public void Clear()
    {
        _sweepTimer?.Stop();
        _sweepTimer?.Dispose();
        _sweepTimer = null;

        DispatchToMainThread(() =>
        {
            _latestNonChatEvent = null;
            Checkpoints.Clear();
        });
    }

    #region Helpers

    /// <summary>
    /// Ensures ObservableCollection mutations happen on the UI thread.
    /// If already on MainThread, executes synchronously to avoid deadlocks.
    /// </summary>
    private static void DispatchToMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    /// <summary>
    /// Gets or creates a minute-based bucket for the given timestamp.
    /// If the current (newest) checkpoint is in the same minute, reuse it.
    /// Otherwise create a new one and compress the previous bucket's non-chat events.
    /// </summary>
    private TimelineCheckpoint GetOrCreateBucket(DateTime eventTimestamp)
    {
        var minute = TruncateToMinute(eventTimestamp);
        var current = CurrentCheckpoint;

        if (current != null && current.BucketMinute == minute)
        {
            return current;
        }

        // Compress the outgoing bucket before creating a new one
        if (current != null)
        {
            CompressOlderBucket(current);
        }

        // New minute — create a new bucket
        var context = _sessionManager.CurrentState;
        var checkpoint = new TimelineCheckpoint
        {
            Context = context,
            BucketMinute = minute,
            Timestamp = eventTimestamp,
        };

        Checkpoints.Insert(0, checkpoint);
        CheckpointCreated?.Invoke(this, checkpoint);
        PersistCheckpointIfActive(checkpoint);
        return checkpoint;
    }

    /// <summary>
    /// Compresses non-chat events in a bucket that is no longer current.
    /// Merges same-type non-chat EventLines into grouped lines (chronological order),
    /// then collapses all but the newest per group.
    /// DirectMessage EventLines are never compressed or merged.
    /// </summary>
    private static void CompressOlderBucket(TimelineCheckpoint bucket)
    {
        // Collect all non-chat events by type, in chronological order (oldest first)
        var groupedByType = new Dictionary<EventOutputType, List<TimelineEvent>>();
        var chatLines = new List<EventLine>();

        foreach (var line in bucket.EventLines)
        {
            if (line.IsDirectChat)
            {
                chatLines.Add(line);
                continue;
            }

            if (!groupedByType.TryGetValue(line.OutputType, out var list))
            {
                list = new List<TimelineEvent>();
                groupedByType[line.OutputType] = list;
            }

            foreach (var evt in line.Events)
            {
                list.Add(evt);
            }
        }

        // Sort each group by timestamp (newest first) so index 0 = newest
        foreach (var list in groupedByType.Values)
        {
            list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        }

        // Rebuild EventLines in deterministic newest-first chronology.
        var rebuiltLines = new List<(EventLine line, DateTime newestTimestamp)>();

        foreach (var chatLine in chatLines)
        {
            var newestChatTimestamp = chatLine.Events.Max(e => e.Timestamp);
            rebuiltLines.Add((chatLine, newestChatTimestamp));
        }

        foreach (var (type, events) in groupedByType)
        {
            var mergedLine = new EventLine
            {
                OutputType = type,
                Events = new ObservableCollection<TimelineEvent>(events),
            };
            bucket.EventLines.Add(mergedLine);

            // Collapse all but the first (index 0 = newest) event
            for (int i = 1; i < mergedLine.Events.Count; i++)
            {
                mergedLine.Events[i].IsExpanded = false;
            }

            rebuiltLines.Add((mergedLine, mergedLine.Events.First().Timestamp));
        }

        bucket.EventLines.Clear();

        foreach (var (line, _) in rebuiltLines
                     .OrderByDescending(x => x.newestTimestamp))
        {
            bucket.EventLines.Add(line);
        }
    }

    /// <summary>
    /// Persists a checkpoint to the history database if a session is active.
    /// Fire-and-forget: SessionHistoryService has its own try-catch.
    /// </summary>
    private void PersistCheckpointIfActive(TimelineCheckpoint checkpoint)
    {
        if (_historyService is null || _sessionTrace?.SessionId is not { } sid) return;
        var order = Interlocked.Increment(ref _checkpointDisplayOrder);
        _ = _historyService.PersistTimelineCheckpointAsync(sid, checkpoint, order);
    }

    private static DateTime TruncateToMinute(DateTime dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Kind);

    #endregion
}
