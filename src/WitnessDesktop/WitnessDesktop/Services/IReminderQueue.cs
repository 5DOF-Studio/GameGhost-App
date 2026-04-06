using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

public interface IReminderQueue
{
    int Count { get; }
    void Enqueue(ReminderItem item);
    ReminderItem? PeekMostRelevant();
    ReminderItem? Dequeue();
    void PruneStale(TimeSpan maxAge);
    void Supersede(BargeInCategory category, ReminderItem replacement);
}
