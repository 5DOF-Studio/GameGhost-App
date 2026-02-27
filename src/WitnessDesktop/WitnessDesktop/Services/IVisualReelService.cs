using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Service for appending and querying visual reel moments (captured frames).
/// MVP: in-memory with rolling retention; no persistence.
/// </summary>
public interface IVisualReelService
{
    /// <summary>
    /// Appends a moment to the reel. Subject to retention policy (time/count).
    /// </summary>
    void Append(ReelMoment moment);

    /// <summary>
    /// Returns the most recent moments, newest first.
    /// </summary>
    /// <param name="count">Maximum number of moments to return.</param>
    IReadOnlyList<ReelMoment> GetRecent(int count = 50);
}
