using WitnessDesktop.Models.Exchange;

namespace WitnessDesktop.Services;

/// <summary>
/// Bounded reminder queue with staleness pruning and category supersession.
/// Thread-safe. Cap: 10 items. Freshest-first dequeue.
/// Spec Section 8.4: stale → dropped, superseded → replaced.
/// </summary>
public sealed class ReminderQueue : IReminderQueue
{
    public const int MaxItems = 10;
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(5);

    private readonly List<ReminderItem> _items = new();
    private readonly object _lock = new();
    private readonly TimeSpan _maxAge;

    public ReminderQueue(TimeSpan? maxAge = null)
    {
        _maxAge = maxAge ?? DefaultMaxAge;
    }

    public int Count
    {
        get { lock (_lock) return _items.Count; }
    }

    public void Enqueue(ReminderItem item)
    {
        lock (_lock)
        {
            _items.Add(item);
            // Enforce cap — drop oldest when over
            while (_items.Count > MaxItems)
                _items.RemoveAt(0);
            // Prune after adding so items stale at enqueue time are also dropped
            PruneStale_NoLock(_maxAge);
        }
    }

    public ReminderItem? PeekMostRelevant()
    {
        lock (_lock)
        {
            PruneStale_NoLock(_maxAge);
            // Priority: category (lower enum = higher priority), then freshest
            return _items
                .Where(r => !r.IsSuperseded)
                .OrderBy(r => r.Category)
                .ThenByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
        }
    }

    public ReminderItem? Dequeue()
    {
        lock (_lock)
        {
            PruneStale_NoLock(_maxAge);
            var item = _items
                .Where(r => !r.IsSuperseded)
                .OrderBy(r => r.Category)
                .ThenByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
            if (item != null)
                _items.Remove(item);
            return item;
        }
    }

    public void PruneStale(TimeSpan maxAge)
    {
        lock (_lock) PruneStale_NoLock(maxAge);
    }

    public void Supersede(BargeInCategory category, ReminderItem replacement)
    {
        lock (_lock)
        {
            foreach (var existing in _items.Where(r => r.Category == category && !r.IsSuperseded))
                existing.IsSuperseded = true;
            // Remove superseded items before adding replacement so cap check is accurate
            PruneStale_NoLock(_maxAge);
            _items.Add(replacement);
            while (_items.Count > MaxItems)
                _items.RemoveAt(0);
        }
    }

    private void PruneStale_NoLock(TimeSpan maxAge)
    {
        var now = DateTime.UtcNow;
        _items.RemoveAll(r => r.IsStale(now, maxAge) || r.IsSuperseded);
    }
}
