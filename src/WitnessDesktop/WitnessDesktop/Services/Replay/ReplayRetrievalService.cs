using Microsoft.EntityFrameworkCore;
using WitnessDesktop.Data;

namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Composes data from the history DB (messages, events) and the observation store (captures)
/// into a single chronologically-ordered ReplayContext.
/// </summary>
public sealed class ReplayRetrievalService : IReplayRetrievalService
{
    private static readonly TimeSpan MaxWindow = TimeSpan.FromMinutes(5);

    private readonly string _dbPath;
    private readonly IObservationStore _observationStore;

    public ReplayRetrievalService(string dbPath, IObservationStore observationStore)
    {
        _dbPath = dbPath;
        _observationStore = observationStore;
    }

    public async Task<ReplayContext> GetByTimeWindowAsync(
        string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
    {
        // Clamp window to max 5 minutes (move startUtc forward if needed)
        if (endUtc - startUtc > MaxWindow)
            startUtc = endUtc - MaxWindow;

        var items = new List<ReplayItem>();

        // 1. Query chat messages from history DB
        using (var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath))
        {
            var messages = await ctx.ChatMessages
                .Where(m => m.SessionId == sessionId
                         && m.TimestampUtc >= startUtc
                         && m.TimestampUtc <= endUtc)
                .OrderBy(m => m.TimestampUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var msg in messages)
            {
                items.Add(new ReplayItem
                {
                    TimestampUtc = msg.TimestampUtc,
                    Kind = ReplayItemKind.ChatMessage,
                    MessageContent = msg.Content,
                    MessageRole = msg.Role
                });
            }

            // 2. Query timeline events from history DB
            var events = await ctx.TimelineEvents
                .Where(e => e.SessionId == sessionId
                         && e.CreatedAtUtc >= startUtc
                         && e.CreatedAtUtc <= endUtc)
                .OrderBy(e => e.CreatedAtUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var evt in events)
            {
                items.Add(new ReplayItem
                {
                    TimestampUtc = evt.CreatedAtUtc,
                    Kind = ReplayItemKind.TimelineEvent,
                    EventSummary = evt.Summary,
                    EventType = evt.Type,
                    BrainSignal = evt.BrainSignal,
                    SuggestedAction = evt.SuggestedAction
                });
            }
        }

        // 3. Query observation artifacts
        var observations = await _observationStore
            .GetByTimeRangeAsync(sessionId, startUtc, endUtc, ct)
            .ConfigureAwait(false);

        foreach (var obs in observations)
        {
            items.Add(new ReplayItem
            {
                TimestampUtc = obs.CapturedAtUtc,
                Kind = ReplayItemKind.CaptureArtifact,
                ArtifactPath = obs.ArtifactPath,
                ArtifactExists = File.Exists(obs.ArtifactPath)
            });
        }

        // 4. Merge all items in chronological order
        items.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));

        return new ReplayContext
        {
            SessionId = sessionId,
            WindowStartUtc = startUtc,
            WindowEndUtc = endUtc,
            Items = items
        };
    }

    public Task<ReplayContext> GetRecentAsync(
        string sessionId, TimeSpan lookback, CancellationToken ct = default)
    {
        var endUtc = DateTime.UtcNow;
        var startUtc = endUtc - lookback;
        return GetByTimeWindowAsync(sessionId, startUtc, endUtc, ct);
    }
}
