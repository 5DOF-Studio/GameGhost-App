namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Managed wrapper interface over GaimerSpeech native methods.
/// Provides safe async APIs for speech-to-text and text-to-speech
/// via the GaimerSpeech.xcframework.
///
/// Injectable seam: enables testing Mac providers on net8.0 without native code.
/// Production implementation: <see cref="GaimerSpeechInterop"/> (Mac Catalyst only).
/// </summary>
public interface IGaimerSpeechInterop
{
    /// <summary>Whether native STT (SFSpeechRecognizer) is available.</summary>
    bool IsSttAvailable { get; }

    /// <summary>Whether native TTS (AVSpeechSynthesizer) is available.</summary>
    bool IsTtsAvailable { get; }

    /// <summary>
    /// Transcribe PCM audio (16kHz, 16-bit, mono) into text.
    /// </summary>
    Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default);

    /// <summary>
    /// Synthesize text into PCM audio (24kHz, 16-bit, mono).
    /// </summary>
    Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default);
}
