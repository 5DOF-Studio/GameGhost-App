namespace WitnessDesktop.Services.Local;

/// <summary>
/// Response DTO from local vision inference.
/// Contains the assistant's analysis text and optional structured fields for BrainResult construction.
/// </summary>
public sealed class LocalVisionResponse
{
    /// <summary>The assistant's full analysis text.</summary>
    public required string AssistantText { get; init; }

    /// <summary>Whether the inference completed successfully.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Failure reason when Success is false.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Model confidence level (0.0–1.0), if reported by the runtime.</summary>
    public double? Confidence { get; init; }

    /// <summary>Model identifier that actually served the request.</summary>
    public string? ModelId { get; init; }

    /// <summary>Inference latency in milliseconds, if measured by the client.</summary>
    public int? LatencyMs { get; init; }
}
