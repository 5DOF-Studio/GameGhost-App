using System.Collections.ObjectModel;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

public class TimelineFeed : ITimelineFeed
{
    private readonly TimeSpan _retentionWindow;
    private readonly int _sweepIntervalMs;
    private System.Timers.Timer? _sweepTimer;
    private TimelineEvent? _latestNonChatEvent;

    public ObservableCollection<TimelineEvent> Events { get; } = new();

    public event EventHandler<TimelineEvent>? EventAdded;

    public TimelineFeed(
        TimeSpan? retentionWindow = null,
        int sweepIntervalMs = 60_000)
    {
        _retentionWindow = retentionWindow ?? TimeSpan.FromMinutes(1);
        _sweepIntervalMs = Math.Max(1000, sweepIntervalMs);
    }

    public void AddEvent(TimelineEvent evt)
    {
        DispatchToMainThread(() =>
        {
            // DirectMessage: always expanded, never participates in IsLatest
            if (evt.IsDirectChat)
            {
                evt.IsExpanded = true;
                Events.Insert(0, evt);
                EventAdded?.Invoke(this, evt);
                return;
            }

            // Non-chat: mark as latest, expanded
            evt.IsExpanded = true;
            evt.IsLatest = true;

            if (_latestNonChatEvent != null)
            {
                _latestNonChatEvent.IsLatest = false;
            }
            _latestNonChatEvent = evt;

            Events.Insert(0, evt);
            EventAdded?.Invoke(this, evt);
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

    public void InsertArchiveBoundary()
    {
        DispatchToMainThread(() =>
        {
            // Remove any existing archive sentinel first (only one at a time)
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].Type == EventOutputType.Archived)
                {
                    Events.RemoveAt(i);
                    break;
                }
            }

            // Append at end = bottom of newest-first feed
            Events.Add(new TimelineEvent
            {
                Type = EventOutputType.Archived,
                Summary = "Archived",
                Icon = EventIconMap.GetIcon(EventOutputType.Archived),
                CapsuleColorHex = EventIconMap.GetCapsuleColorHex(EventOutputType.Archived),
                CapsuleStrokeHex = EventIconMap.GetCapsuleStrokeHex(EventOutputType.Archived),
            });
        });
    }

    /// <summary>
    /// Prunes events older than the retention window from the list.
    /// When events are pruned, an archive boundary sentinel is appended.
    /// Called by the sweep timer every <see cref="_sweepIntervalMs"/> milliseconds.
    /// </summary>
    internal void EnforceRetention()
    {
        DispatchToMainThread(() =>
        {
            var cutoff = DateTime.UtcNow - _retentionWindow;
            var removedAny = false;

            for (int i = Events.Count - 1; i >= 0; i--)
            {
                var evt = Events[i];

                // Never remove the archive sentinel itself
                if (evt.Type == EventOutputType.Archived) continue;

                if (evt.Timestamp < cutoff)
                {
                    // Clear latest ref if it's being removed
                    if (evt == _latestNonChatEvent)
                    {
                        _latestNonChatEvent = null;
                    }

                    Events.RemoveAt(i);
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
            Events.Clear();
        });
    }

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
}
