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
    private readonly ISessionTraceService? _sessionTrace;

    public ReminderQueue(TimeSpan? maxAge = null, ISessionTraceService? sessionTrace = null)
    {
        _maxAge = maxAge ?? DefaultMaxAge;
        _sessionTrace = sessionTrace;
    }

    public int Count
    {
        get { lock (_lock) return _items.Count; }
    }

    public void Enqueue(ReminderItem item)
    {
        int depth;
        lock (_lock)
        {
            _items.Add(item);
            // Enforce cap — drop oldest when over
            while (_items.Count > MaxItems)
                _items.RemoveAt(0);
            // Prune after adding so items stale at enqueue time are also dropped
            PruneStale_NoLock(_maxAge);
            depth = _items.Count;
        }
        _sessionTrace?.TrackEvent("voice.reminder.enqueued", new Dictionary<string, string>
        {
            ["category"] = item.Category.ToString(),
            ["queue_depth"] = depth.ToString()
        });
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
        ReminderItem? item;
        lock (_lock)
        {
            PruneStale_NoLock(_maxAge);
            item = _items
                .Where(r => !r.IsSuperseded)
                .OrderBy(r => r.Category)
                .ThenByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
            if (item != null)
                _items.Remove(item);
        }
        if (item != null)
        {
            var ageMs = (long)(DateTime.UtcNow - item.CreatedAtUtc).TotalMilliseconds;
            _sessionTrace?.TrackEvent("voice.reminder.dequeued", new Dictionary<string, string>
            {
                ["category"] = item.Category.ToString(),
                ["age_ms"] = ageMs.ToString()
            });
        }
        return item;
    }

    public void PruneStale(TimeSpan maxAge)
    {
        int prunedCount;
        TimeSpan? oldestAge = null;
        lock (_lock)
        {
            var countBefore = _items.Count;
            if (_items.Count > 0)
            {
                var now = DateTime.UtcNow;
                var oldest = _items.Min(r => r.CreatedAtUtc);
                oldestAge = now - oldest;
            }
            PruneStale_NoLock(maxAge);
            prunedCount = countBefore - _items.Count;
        }
        if (prunedCount > 0)
        {
            _sessionTrace?.TrackEvent("voice.reminder.pruned", new Dictionary<string, string>
            {
                ["count"] = prunedCount.ToString(),
                ["oldest_age_ms"] = ((long)(oldestAge?.TotalMilliseconds ?? 0)).ToString()
            });
        }
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
        _sessionTrace?.TrackEvent("voice.reminder.superseded", new Dictionary<string, string>
        {
            ["category"] = category.ToString()
        });
    }

    private void PruneStale_NoLock(TimeSpan maxAge)
    {
        var now = DateTime.UtcNow;
        _items.RemoveAll(r => r.IsStale(now, maxAge) || r.IsSuperseded);
    }
}
