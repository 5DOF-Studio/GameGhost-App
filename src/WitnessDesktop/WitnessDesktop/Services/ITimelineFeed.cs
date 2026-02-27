using System.Collections.ObjectModel;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

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
