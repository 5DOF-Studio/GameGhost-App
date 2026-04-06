namespace WitnessDesktop.Models;

/// <summary>
/// Result of provider policy resolution — describes the resolved inference strategy for the current session.
/// </summary>
public sealed class InferenceSelection
{
    /// <summary>The mode that was resolved.</summary>
    public required InferenceMode Mode { get; init; }

    /// <summary>Whether local brain inference is active.</summary>
    public bool LocalBrainActive { get; init; }

    /// <summary>Whether local voice inference is active.</summary>
    public bool LocalVoiceActive { get; init; }

    /// <summary>Whether cloud providers are being used as fallback.</summary>
    public bool CloudFallbackActive { get; init; }

    /// <summary>Whether the resolved selection can support a session.</summary>
    public bool Available { get; init; }

    /// <summary>Human-readable failure reason when Available is false.</summary>
    public string? FailureReason { get; init; }
}
