namespace WitnessDesktop.Services.Local;

/// <summary>
/// Request DTO for local vision inference.
/// Carries image data and prompt context to the local runtime.
/// </summary>
public sealed class LocalVisionRequest
{
    /// <summary>PNG image bytes from screen capture.</summary>
    public required byte[] ImageData { get; init; }

    /// <summary>User-facing prompt text (e.g. "Current game: chess. Position #5.").</summary>
    public required string UserPrompt { get; init; }

    /// <summary>System prompt with agent personality, context layers, and output format.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>Optional model identifier for runtime routing.</summary>
    public string? ModelId { get; init; }

    /// <summary>Correlation ID for telemetry tracing.</summary>
    public string? CorrelationId { get; init; }
}
