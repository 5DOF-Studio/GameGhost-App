namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Audio-based wake word detector using Picovoice Porcupine.
/// Processes raw 16kHz 16-bit mono PCM frames.
/// Primary wake detection mechanism (D-AI-7); transcript-based fuzzy matching is the fallback.
/// </summary>
public interface IPorcupineWakeDetector : IDisposable
{
    /// <summary>Whether Porcupine is initialized and ready to process audio.</summary>
    bool IsAvailable { get; }

    /// <summary>Process a raw PCM audio frame. Call from the mic capture callback.</summary>
    /// <param name="pcm16Data">16-bit mono PCM audio bytes at 16kHz.</param>
    void ProcessAudio(byte[] pcm16Data);

    /// <summary>Fired when a wake word is detected. Carries the detected keyword name.</summary>
    event EventHandler<string>? WakeWordDetected;
}
