using WitnessDesktop.Data;

namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Resolves an event or message ID to its timestamp, expands a radius window,
/// and delegates to the retrieval service for context assembly.
/// </summary>
public sealed class ReplayAnchorService : IReplayAnchorService
{
    private readonly string _dbPath;
    private readonly IReplayRetrievalService _retrievalService;

    public ReplayAnchorService(string dbPath, IReplayRetrievalService retrievalService)
    {
        _dbPath = dbPath;
        _retrievalService = retrievalService;
    }

    public async Task<ReplayContext?> GetAroundEventAsync(
        string sessionId, string eventId, TimeSpan radius, CancellationToken ct = default)
    {
        DateTime? anchor;

        using (var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath))
        {
            ctx.Database.EnsureCreated();
            var evt = await ctx.TimelineEvents.FindAsync(new object[] { eventId }, ct).ConfigureAwait(false);
            if (evt == null)
                return null;

            // Validate the event belongs to the requested session
            if (!string.Equals(evt.SessionId, sessionId, StringComparison.Ordinal))
                return null;

            anchor = evt.CreatedAtUtc;
        }

        var startUtc = anchor.Value - radius;
        var endUtc = anchor.Value + radius;

        return await _retrievalService
            .GetByTimeWindowAsync(sessionId, startUtc, endUtc, ct)
            .ConfigureAwait(false);
    }

    public async Task<ReplayContext?> GetAroundMessageAsync(
        string sessionId, string messageId, TimeSpan radius, CancellationToken ct = default)
    {
        DateTime? anchor;

        using (var ctx = GaimerHistoryDbContext.CreateForPath(_dbPath))
        {
            ctx.Database.EnsureCreated();
            var msg = await ctx.ChatMessages.FindAsync(new object[] { messageId }, ct).ConfigureAwait(false);
            if (msg == null)
                return null;

            // Validate the message belongs to the requested session
            if (!string.Equals(msg.SessionId, sessionId, StringComparison.Ordinal))
                return null;

            anchor = msg.TimestampUtc;
        }

        var startUtc = anchor.Value - radius;
        var endUtc = anchor.Value + radius;

        return await _retrievalService
            .GetByTimeWindowAsync(sessionId, startUtc, endUtc, ct)
            .ConfigureAwait(false);
    }
}
