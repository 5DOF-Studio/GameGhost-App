namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Playback-only audio service abstraction.
/// </summary>
/// <remarks>
/// This interface exists to allow platform implementations (notably MacCatalyst)
/// to run AI audio playback in a dedicated engine, isolated from microphone capture.
/// </remarks>
public interface IAudioPlaybackService : IDisposable
{
    bool IsPlaying { get; }
    float OutputVolume { get; }

    event EventHandler<float>? OutputVolumeChanged;
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    Task PlayAudioAsync(byte[] pcmData);
    Task StopPlaybackAsync();
    Task InterruptPlaybackAsync();
}


