namespace WitnessDesktop.Services.Local;

/// <summary>
/// Transport abstraction for local multimodal inference.
/// Implementers handle the actual runtime communication (e.g. Ollama HTTP, direct process).
/// </summary>
public interface ILocalVisionInferenceClient
{
    /// <summary>
    /// Analyze an image with a multimodal local model.
    /// </summary>
    Task<LocalVisionResponse> AnalyzeImageAsync(LocalVisionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Text-only chat with the local model (no image).
    /// Used for out-of-game conversation and query responses.
    /// </summary>
    Task<string> ChatAsync(string userQuery, string systemPrompt, CancellationToken ct = default);
}
