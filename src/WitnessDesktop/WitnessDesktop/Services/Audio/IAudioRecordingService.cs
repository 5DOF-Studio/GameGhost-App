namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Recording-only audio service abstraction.
/// </summary>
/// <remarks>
/// This interface exists to allow platform implementations (notably MacCatalyst)
/// to run microphone capture in a dedicated engine, isolated from playback.
/// </remarks>
public interface IAudioRecordingService : IDisposable
{
    bool IsRecording { get; }
    float InputVolume { get; }

    event EventHandler<float>? InputVolumeChanged;
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    Task StartRecordingAsync(Action<byte[]> onAudioCaptured);
    Task StopRecordingAsync();
}


