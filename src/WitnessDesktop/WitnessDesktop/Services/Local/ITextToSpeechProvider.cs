namespace WitnessDesktop.Services.Local;

/// <summary>
/// Abstraction for text-to-speech synthesis.
/// Accepts response text, returns PCM audio in the standard output format.
/// Implementations: platform-native (Mac AVSpeechSynthesizer), future local vocoder, mock/stub.
/// </summary>
public interface ITextToSpeechProvider
{
    /// <summary>Whether this provider is ready to synthesize speech.</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable name of the TTS engine (e.g. "Apple TTS", "Piper").</summary>
    string EngineName { get; }

    /// <summary>
    /// Synthesize text into PCM audio.
    /// Returns raw PCM bytes in the standard output format (16-bit, 24kHz mono).
    /// </summary>
    /// <param name="text">Text to synthesize.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>PCM audio bytes, or null if synthesis failed.</returns>
    Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default);
}
