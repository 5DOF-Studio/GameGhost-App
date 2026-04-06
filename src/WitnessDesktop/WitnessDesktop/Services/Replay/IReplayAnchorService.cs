namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Resolves event or message IDs to time windows and retrieves surrounding context.
/// </summary>
public interface IReplayAnchorService
{
    /// <summary>
    /// Finds the event's timestamp, expands +/- <paramref name="radius"/>, and retrieves context.
    /// Returns null if the event is not found.
    /// </summary>
    Task<ReplayContext?> GetAroundEventAsync(
        string sessionId, string eventId, TimeSpan radius, CancellationToken ct = default);

    /// <summary>
    /// Finds the message's timestamp, expands +/- <paramref name="radius"/>, and retrieves context.
    /// Returns null if the message is not found.
    /// </summary>
    Task<ReplayContext?> GetAroundMessageAsync(
        string sessionId, string messageId, TimeSpan radius, CancellationToken ct = default);
}
