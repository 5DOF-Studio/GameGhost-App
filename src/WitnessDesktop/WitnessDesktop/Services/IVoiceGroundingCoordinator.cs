using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// App-owned grounding coordinator that decides when voice is allowed to make
/// board-state claims and when it must express uncertainty.
/// Sits above conversation providers — providers remain transport-focused.
/// </summary>
public interface IVoiceGroundingCoordinator
{
    /// <summary>
    /// Update the grounded fact cache with fresh brain-derived context.
    /// Called when BrainEventRouter routes a structured or fallback analysis result.
    /// </summary>
    void UpdateGroundedContext(GroundedVoiceContext context);

    /// <summary>
    /// Classify a user turn and produce a grounding decision.
    /// </summary>
    /// <param name="userText">The user's text or transcript.</param>
    /// <param name="isInGame">Whether the session is currently in-game.</param>
    VoiceGroundingDecision Evaluate(string? userText, bool isInGame);

    /// <summary>
    /// The latest grounded context, or null if none has been received.
    /// </summary>
    GroundedVoiceContext? LatestContext { get; }

    /// <summary>
    /// Formats a grounding prefix for injection into voice contextual updates.
    /// Returns null if no grounded context is available.
    /// </summary>
    string? GetGroundingPrefix();
}
