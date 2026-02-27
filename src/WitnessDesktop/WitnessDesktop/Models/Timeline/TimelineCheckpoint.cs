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
    
    public string HeaderIcon => Context == SessionState.InGame ? "video_reel.png" : "history_clock.png";

    public string DisplayHeader
    {
        get
        {
            if (Context == SessionState.InGame && GameTimeIn.HasValue)
            {
                return $"Capture — {GameTimeIn.Value:m\\:ss} in";
            }
            else
            {
                return $"{Timestamp:h:mm tt}";
            }
        }
    }

    public string ContextBadge => Context == SessionState.InGame ? "[in-game]" : "[out-game]";
}
