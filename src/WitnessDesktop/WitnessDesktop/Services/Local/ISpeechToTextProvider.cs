namespace WitnessDesktop.Services.Local;

/// <summary>
/// Abstraction for speech-to-text conversion.
/// Accepts PCM audio input, returns completed transcript per turn.
/// Implementations: platform-native (Mac SFSpeechRecognizer), future Whisper/ONNX, mock/stub.
/// </summary>
public interface ISpeechToTextProvider
{
    /// <summary>Whether this provider is ready to accept audio and produce transcripts.</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable name of the STT engine (e.g. "Apple Speech", "Whisper").</summary>
    string EngineName { get; }

    /// <summary>
    /// Transcribe PCM audio data into text.
    /// Returns the completed transcript for the audio turn.
    /// </summary>
    /// <param name="pcmAudio">Raw PCM audio bytes in the standard input format (16-bit, 16kHz mono).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Transcribed text, or null/empty if no speech detected.</returns>
    Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default);
}
