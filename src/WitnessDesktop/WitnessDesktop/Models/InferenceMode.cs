namespace WitnessDesktop.Models;

/// <summary>
/// Determines how the app selects inference providers for brain and voice.
/// </summary>
public enum InferenceMode
{
    /// <summary>Use cloud providers only. Default for backwards compatibility.</summary>
    CloudOnly = 0,

    /// <summary>Use local runtime only. No silent cloud fallback.</summary>
    LocalOnly = 1,

    /// <summary>Prefer local runtime; fall back to cloud when local is unavailable.</summary>
    LocalFirst = 2
}
