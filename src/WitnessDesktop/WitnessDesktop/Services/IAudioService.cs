using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Services;

/// <summary>
/// Platform-agnostic audio service for real-time microphone capture and AI response playback.
/// </summary>
/// <remarks>
/// <para>
/// <b>Audio Format Contract:</b> This service uses the standard formats defined in <see cref="AudioFormat"/>:
/// </para>
/// <list type="bullet">
/// <item><b>Input:</b> <see cref="AudioFormat.StandardInputSampleRate"/> (16kHz), 16-bit, mono PCM</item>
/// <item><b>Output:</b> <see cref="AudioFormat.StandardOutputSampleRate"/> (24kHz), 16-bit, mono PCM</item>
/// </list>
/// <para>
/// Implementations must be thread-safe. Audio callbacks may fire on background threads;
/// consumers should marshal UI updates to the main thread.
/// </para>
/// <para>
/// Idempotency: calling <see cref="StartRecordingAsync"/> when already recording is a no-op;
/// calling <see cref="StopRecordingAsync"/> or <see cref="StopPlaybackAsync"/> when not active is a no-op.
/// </para>
/// </remarks>
public interface IAudioService : IDisposable
{
    /// <summary>
    /// Starts microphone capture. Audio frames are delivered via <paramref name="onAudioCaptured"/>.
    /// </summary>
    /// <param name="onAudioCaptured">
    /// Callback invoked (on a background thread) with each captured PCM frame
    /// at <see cref="AudioFormat.StandardInputSampleRate"/> (16kHz), 16-bit, mono.
    /// Must be fast and non-blocking; do not perform network I/O inside the callback.
    /// </param>
    /// <returns>A task that completes when recording has started (or failed).</returns>
    Task StartRecordingAsync(Action<byte[]> onAudioCaptured);

    /// <summary>
    /// Stops microphone capture. Safe to call when not recording.
    /// </summary>
    Task StopRecordingAsync();

    /// <summary>
    /// True while the microphone is actively capturing audio.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Queues PCM audio data for playback through the default output device.
    /// </summary>
    /// <param name="pcmData">
    /// Audio bytes at <see cref="AudioFormat.StandardOutputSampleRate"/> (24kHz), 16-bit, mono PCM.
    /// </param>
    Task PlayAudioAsync(byte[] pcmData);

    /// <summary>
    /// Stops playback gracefully. Safe to call when not playing.
    /// </summary>
    Task StopPlaybackAsync();

    /// <summary>
    /// Immediately stops playback and clears any buffered audio.
    /// Use when the user interrupts the AI (e.g., starts speaking).
    /// </summary>
    Task InterruptPlaybackAsync();

    /// <summary>
    /// True while audio is being played back.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Most recent input (microphone) RMS volume in the range [0..1].
    /// </summary>
    float InputVolume { get; }

    /// <summary>
    /// Most recent output (playback) RMS volume in the range [0..1].
    /// </summary>
    float OutputVolume { get; }

    /// <summary>
    /// Input sample rate in Hz. Returns <see cref="AudioFormat.StandardInputSampleRate"/> (16000).
    /// </summary>
    int InputSampleRate { get; }

    /// <summary>
    /// Output sample rate in Hz. Returns <see cref="AudioFormat.StandardOutputSampleRate"/> (24000).
    /// </summary>
    int OutputSampleRate { get; }

    /// <summary>
    /// Raised whenever input or output volume changes (on a background thread).
    /// </summary>
    event EventHandler<VolumeChangedEventArgs>? VolumeChanged;

    /// <summary>
    /// Raised when an audio error occurs. Check <see cref="AudioErrorEventArgs.IsFatal"/>
    /// to determine if the error is recoverable.
    /// </summary>
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
}
