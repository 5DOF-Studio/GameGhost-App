using System.Collections.ObjectModel;

namespace WitnessDesktop.Models.Timeline;

public class EventLine
{
    public EventOutputType OutputType { get; set; }
    public ObservableCollection<TimelineEvent> Events { get; set; } = new();
    
    public string LineIcon => Events.FirstOrDefault()?.Icon ?? EventIconMap.GetIcon(OutputType);
    
    public bool HasMultipleEvents => Events.Count > 1;
}
