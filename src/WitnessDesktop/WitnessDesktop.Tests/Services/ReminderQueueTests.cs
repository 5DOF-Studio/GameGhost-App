using FluentAssertions;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class ReminderQueueTests
{
    [Fact]
    public void InitialState_IsEmpty()
    {
        var sut = new ReminderQueue();
        sut.Count.Should().Be(0);
        sut.Dequeue().Should().BeNull();
    }

    [Fact]
    public void Enqueue_Dequeue_ReturnsSameItem()
    {
        var sut = new ReminderQueue();
        var item = MakeReminder("test", BargeInCategory.Reminder);
        sut.Enqueue(item);
        sut.Dequeue().Should().Be(item);
    }

    [Fact]
    public void Dequeue_ReturnsFreshestFirst()
    {
        var sut = new ReminderQueue();
        var old = MakeReminder("old", BargeInCategory.Reminder, DateTime.UtcNow.AddMinutes(-2));
        var fresh = MakeReminder("fresh", BargeInCategory.Reminder);
        sut.Enqueue(old);
        sut.Enqueue(fresh);
        sut.Dequeue()!.Content.Should().Be("fresh");
    }

    [Fact]
    public void Enqueue_PrunesStaleItems()
    {
        var sut = new ReminderQueue(maxAge: TimeSpan.FromMilliseconds(50));
        sut.Enqueue(MakeReminder("stale", BargeInCategory.Reminder, DateTime.UtcNow.AddSeconds(-1)));
        sut.Count.Should().Be(0);
    }

    [Fact]
    public void Supersede_ReplacesSameCategory()
    {
        var sut = new ReminderQueue();
        sut.Enqueue(MakeReminder("old callout", BargeInCategory.CallOut));
        sut.Supersede(BargeInCategory.CallOut, MakeReminder("new callout", BargeInCategory.CallOut));
        sut.Count.Should().Be(1);
        sut.Dequeue()!.Content.Should().Be("new callout");
    }

    [Fact]
    public void Supersede_DoesNotAffectOtherCategories()
    {
        var sut = new ReminderQueue();
        sut.Enqueue(MakeReminder("reminder", BargeInCategory.Reminder));
        sut.Supersede(BargeInCategory.CallOut, MakeReminder("callout", BargeInCategory.CallOut));
        sut.Count.Should().Be(2);
    }

    [Fact]
    public void Enqueue_RespectsCapOf10()
    {
        var sut = new ReminderQueue();
        for (int i = 0; i < 15; i++)
            sut.Enqueue(MakeReminder($"item-{i}", BargeInCategory.FreeCommentary));
        sut.Count.Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public void PeekMostRelevant_DoesNotRemoveItem()
    {
        var sut = new ReminderQueue();
        sut.Enqueue(MakeReminder("peek me", BargeInCategory.Reminder));
        sut.PeekMostRelevant().Should().NotBeNull();
        sut.Count.Should().Be(1);
    }

    [Fact]
    public void PruneStale_RemovesExpiredItems()
    {
        var sut = new ReminderQueue();
        sut.Enqueue(MakeReminder("old", BargeInCategory.Reminder, DateTime.UtcNow.AddMinutes(-10)));
        sut.Enqueue(MakeReminder("fresh", BargeInCategory.Reminder));
        sut.PruneStale(TimeSpan.FromMinutes(5));
        sut.Count.Should().Be(1);
        sut.Dequeue()!.Content.Should().Be("fresh");
    }

    private static ReminderItem MakeReminder(string content, BargeInCategory category, DateTime? created = null)
        => new() { Content = content, Category = category, CreatedAtUtc = created ?? DateTime.UtcNow };
}
