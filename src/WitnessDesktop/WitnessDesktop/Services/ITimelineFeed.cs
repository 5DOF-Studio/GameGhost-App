using System.Collections.ObjectModel;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

public interface ITimelineFeed
{
    ObservableCollection<TimelineCheckpoint> Checkpoints { get; }
    
    TimelineCheckpoint? CurrentCheckpoint { get; }
    
    void NewCapture(string screenshotRef, TimeSpan gameTime, string method);
    
    TimelineCheckpoint NewConversationCheckpoint();
    
    void AddEvent(TimelineEvent evt);

    /// <summary>
    /// Inserts a grey "Archived" boundary marker into the current checkpoint.
    /// Presentation seam for the future retention engine.
    /// </summary>
    void InsertArchiveBoundary();

    void Clear();
    
    event EventHandler<TimelineCheckpoint>? CheckpointCreated;
}
