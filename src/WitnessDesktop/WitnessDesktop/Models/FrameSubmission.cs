namespace WitnessDesktop.Models;

/// <summary>
/// Immutable record representing a captured frame submitted to the brain pipeline.
/// Carries image data, game context, and timestamp for queue-wait telemetry.
/// </summary>
public record FrameSubmission(byte[] ImageData, string Context, DateTime CapturedAt);
