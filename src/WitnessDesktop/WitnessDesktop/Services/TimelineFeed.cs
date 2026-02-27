using System.Collections.ObjectModel;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

public class TimelineFeed : ITimelineFeed
{
    private readonly ISessionManager _sessionManager;

    public ObservableCollection<TimelineCheckpoint> Checkpoints { get; } = new();

    public TimelineCheckpoint? CurrentCheckpoint => Checkpoints.LastOrDefault();

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
            Checkpoints.Add(checkpoint);
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
            Checkpoints.Add(checkpoint);
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
                checkpoint.EventLines.Add(new EventLine
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
                Checkpoints.Add(checkpoint);
                CheckpointCreated?.Invoke(this, checkpoint);
                return checkpoint;
            }
            else
            {
                var checkpoint = new TimelineCheckpoint
                {
                    Context = SessionState.OutGame,
                };
                Checkpoints.Add(checkpoint);
                CheckpointCreated?.Invoke(this, checkpoint);
                return checkpoint;
            }
        }

        return Checkpoints.Last();
    }

    #endregion
}
