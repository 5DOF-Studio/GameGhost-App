namespace WitnessDesktop.Services.Replay;

/// <summary>
/// Stub implementation of IReplayMediaPresentationService.
/// Returns empty results until Phase E UI integration.
/// </summary>
public sealed class ReplayMediaPresentationService : IReplayMediaPresentationService
{
    public Task<IReadOnlyList<ReplayMediaCard>> GetMediaCardsAsync(
        ReplayContext context, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ReplayMediaCard>>(Array.Empty<ReplayMediaCard>());
    }
}
