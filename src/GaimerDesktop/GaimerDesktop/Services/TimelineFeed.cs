using System.Collections.ObjectModel;
using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;

namespace GaimerDesktop.Services;

public class TimelineFeed : ITimelineFeed
{
    private readonly ISessionManager _sessionManager;

    public ObservableCollection<TimelineCheckpoint> Checkpoints { get; } = new();

    public TimelineCheckpoint? CurrentCheckpoint => Checkpoints.FirstOrDefault();

    public event EventHandler<TimelineCheckpoint>? CheckpointCreated;

    public TimelineFeed(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public TimelineCheckpoint NewCapture(string screenshotRef, TimeSpan gameTime, string method)
    {
        var checkpoint = new TimelineCheckpoint
        {
            Context = SessionState.InGame,
            GameTimeIn = gameTime,
            ScreenshotRef = screenshotRef,
            CaptureMethod = method,
        };

        DispatchToMainThread(() =>
        {
            Checkpoints.Insert(0, checkpoint);
            CheckpointCreated?.Invoke(this, checkpoint);
        });

        return checkpoint;
    }

    public TimelineCheckpoint NewConversationCheckpoint()
    {
        var checkpoint = new TimelineCheckpoint
        {
            Context = SessionState.OutGame,
        };

        DispatchToMainThread(() =>
        {
            Checkpoints.Insert(0, checkpoint);
            CheckpointCreated?.Invoke(this, checkpoint);
        });

        return checkpoint;
    }

    public void AddEvent(TimelineEvent evt)
    {
        DispatchToMainThread(() =>
        {
            var checkpoint = EnsureCurrentCheckpoint();

            var existingLine = checkpoint.EventLines
                .FirstOrDefault(l => l.OutputType == evt.Type);

            if (existingLine != null)
            {
                existingLine.Events.Add(evt);
            }
            else
            {
                // Insert at 0 so newest events appear at the top
                checkpoint.EventLines.Insert(0, new EventLine
                {
                    OutputType = evt.Type,
                    Events = new ObservableCollection<TimelineEvent> { evt },
                });
            }
        });
    }

    public void Clear()
    {
        DispatchToMainThread(() => Checkpoints.Clear());
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

    private TimelineCheckpoint EnsureCurrentCheckpoint()
    {
        if (Checkpoints.Count == 0)
        {
            if (_sessionManager.CurrentState == SessionState.InGame)
            {
                var checkpoint = new TimelineCheckpoint
                {
                    Context = SessionState.InGame,
                    GameTimeIn = TimeSpan.Zero,
                    ScreenshotRef = "auto",
                    CaptureMethod = "auto",
                };
                Checkpoints.Insert(0, checkpoint);
                CheckpointCreated?.Invoke(this, checkpoint);
                return checkpoint;
            }
            else
            {
                var checkpoint = new TimelineCheckpoint
                {
                    Context = SessionState.OutGame,
                };
                Checkpoints.Insert(0, checkpoint);
                CheckpointCreated?.Invoke(this, checkpoint);
                return checkpoint;
            }
        }

        return Checkpoints.First();
    }

    #endregion
}
