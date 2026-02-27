using System.Collections.Generic;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// In-memory visual reel with rolling retention (time and count based).
/// MVP: no persistence; deterministic trim on append.
/// </summary>
public sealed class VisualReelService : IVisualReelService
{
    private readonly List<ReelMoment> _moments = [];
    private readonly object _lock = new();

    /// <summary>
    /// Maximum moments to retain. Older moments are dropped when exceeded.
    /// </summary>
    public int MaxCount { get; init; } = 500;

    /// <summary>
    /// Maximum age in seconds. Moments older than this are dropped.
    /// </summary>
    public int MaxAgeSeconds { get; init; } = 300;

    public void Append(ReelMoment moment)
    {
        lock (_lock)
        {
            _moments.Add(moment);
            Trim();
        }
    }

    public IReadOnlyList<ReelMoment> GetRecent(int count = 50)
    {
        lock (_lock)
        {
            var start = Math.Max(0, _moments.Count - count);
            var slice = _moments.Skip(start).ToList();
            slice.Reverse();
            return slice;
        }
    }

    private void Trim()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Max(0, MaxAgeSeconds));

        // Remove by age first
        _moments.RemoveAll(m => m.TimestampUtc < cutoff);

        // Then by count (remove oldest)
        while (_moments.Count > MaxCount)
        {
            _moments.RemoveAt(0);
        }
    }
}
