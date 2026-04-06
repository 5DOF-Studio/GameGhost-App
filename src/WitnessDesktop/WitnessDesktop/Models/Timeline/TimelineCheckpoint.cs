using System.Collections.ObjectModel;

namespace WitnessDesktop.Models.Timeline;

public class TimelineCheckpoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SessionState Context { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? GameTimeIn { get; set; }
    public string? ScreenshotRef { get; set; }
    public string? CaptureMethod { get; set; }
    public ObservableCollection<EventLine> EventLines { get; set; } = new();

    /// <summary>True when this checkpoint is the global archive boundary marker, not a real capture/conversation checkpoint.</summary>
    public bool IsArchiveBoundary { get; set; }

    /// <summary>The UTC minute this bucket represents. All events within this minute share the bucket.</summary>
    public DateTime BucketMinute { get; set; }

    public string HeaderIcon => Context == SessionState.InGame ? "camera.png" : "history_clock.png";

    public string DisplayHeader
    {
        get
        {
            if (IsArchiveBoundary)
                return "Archived";

            return $"{Timestamp.ToLocalTime():h:mm tt}";
        }
    }

    public string ContextBadge => Timestamp.ToLocalTime().ToString("h:mm tt").ToLowerInvariant();
}
