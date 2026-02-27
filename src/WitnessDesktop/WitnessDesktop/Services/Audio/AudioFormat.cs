namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Standard audio format constants for the conversation provider pipeline.
/// </summary>
/// <remarks>
/// <para>
/// All conversation providers MUST normalize their audio output to these formats
/// before emitting audio data. This ensures the audio service can play back audio
/// from any provider without format-specific handling.
/// </para>
/// <para>
/// <b>Input (Microphone → Provider):</b> 16kHz, 16-bit, mono PCM<br/>
/// <b>Output (Provider → Speaker):</b> 24kHz, 16-bit, mono PCM
/// </para>
/// </remarks>
public static class AudioFormat
{
    /// <summary>
    /// Standard sample rate for microphone input sent to providers (16kHz).
    /// </summary>
    public const int StandardInputSampleRate = 16000;

    /// <summary>
    /// Standard sample rate for audio output from providers (24kHz).
    /// </summary>
    public const int StandardOutputSampleRate = 24000;

    /// <summary>
    /// Bit depth for all audio (16-bit signed integer).
    /// </summary>
    public const int BitsPerSample = 16;

    /// <summary>
    /// Bytes per sample (2 bytes for 16-bit audio).
    /// </summary>
    public const int BytesPerSample = BitsPerSample / 8;

    /// <summary>
    /// Number of audio channels (mono).
    /// </summary>
    public const int Channels = 1;

    /// <summary>
    /// MIME type for input audio (16kHz PCM).
    /// </summary>
    public const string InputMimeType = "audio/pcm;rate=16000";

    /// <summary>
    /// MIME type for output audio (24kHz PCM).
    /// </summary>
    public const string OutputMimeType = "audio/pcm;rate=24000";

    /// <summary>
    /// Calculates the number of bytes for a given duration of input audio.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    /// <returns>Number of bytes.</returns>
    public static int InputBytesForDuration(int milliseconds)
        => StandardInputSampleRate * BytesPerSample * Channels * milliseconds / 1000;

    /// <summary>
    /// Calculates the number of bytes for a given duration of output audio.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    /// <returns>Number of bytes.</returns>
    public static int OutputBytesForDuration(int milliseconds)
        => StandardOutputSampleRate * BytesPerSample * Channels * milliseconds / 1000;
}

