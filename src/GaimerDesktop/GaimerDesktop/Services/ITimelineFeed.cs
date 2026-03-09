using System.Collections.ObjectModel;
using GaimerDesktop.Models.Timeline;

namespace GaimerDesktop.Services;

public interface ITimelineFeed
{
    ObservableCollection<TimelineCheckpoint> Checkpoints { get; }
    
    TimelineCheckpoint? CurrentCheckpoint { get; }
    
    TimelineCheckpoint NewCapture(string screenshotRef, TimeSpan gameTime, string method);
    
    TimelineCheckpoint NewConversationCheckpoint();
    
    void AddEvent(TimelineEvent evt);
    
    void Clear();
    
    event EventHandler<TimelineCheckpoint>? CheckpointCreated;
}
