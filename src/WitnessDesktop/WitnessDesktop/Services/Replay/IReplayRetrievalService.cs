namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Retrieves time-windowed multimodal context from both the history DB
/// and the observation store, merged into a single chronological sequence.
/// </summary>
public interface IReplayRetrievalService
{
    /// <summary>
    /// Returns all chat messages, timeline events, and observation artifacts
    /// within the specified time window for the given session.
    /// Window is clamped to a maximum of 5 minutes.
    /// </summary>
    Task<ReplayContext> GetByTimeWindowAsync(
        string sessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);

    /// <summary>
    /// Convenience method: returns items from the last <paramref name="lookback"/> duration.
    /// Lookback is clamped to a maximum of 5 minutes.
    /// </summary>
    Task<ReplayContext> GetRecentAsync(
        string sessionId, TimeSpan lookback, CancellationToken ct = default);
}
