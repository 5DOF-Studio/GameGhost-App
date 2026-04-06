namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Transforms a ReplayContext into media cards suitable for UI presentation.
/// Phase E placeholder — stub returns empty results until UI integration.
/// </summary>
public interface IReplayMediaPresentationService
{
    /// <summary>
    /// Converts replay context items into presentable media cards.
    /// </summary>
    Task<IReadOnlyList<ReplayMediaCard>> GetMediaCardsAsync(
        ReplayContext context, CancellationToken ct = default);
}

/// <summary>
/// A single card in the replay media presentation.
/// </summary>
public sealed class ReplayMediaCard
{
    public string Title { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Path to capture image, or null if artifact has expired.
    /// </summary>
    public string? ImagePath { get; init; }

    /// <summary>
    /// Event summary or message content for the card.
    /// </summary>
    public string? Description { get; init; }

    public ReplayMediaCardKind Kind { get; init; }
}

/// <summary>
/// Kind of replay media card.
/// </summary>
public enum ReplayMediaCardKind
{
    Image,
    EventSummary
}
