using System.Collections.ObjectModel;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Services;

public interface ITimelineFeed
{
    ObservableCollection<TimelineEvent> Events { get; }

    void AddEvent(TimelineEvent evt);

    void InsertArchiveBoundary();

    void Clear();

    event EventHandler<TimelineEvent>? EventAdded;
}
