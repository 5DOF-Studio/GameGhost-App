using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Audio;

/// <summary>
/// Composite implementation that preserves the existing <see cref="IAudioService"/> contract
/// while delegating to separate recording and playback services.
/// </summary>
public sealed class CompositeAudioService : IAudioService
{
    private readonly IAudioRecordingService _recording;
    private readonly IAudioPlaybackService _playback;
    private readonly object _gate = new();

    private float _inputVolume;
    private float _outputVolume;

    public CompositeAudioService(IAudioRecordingService recording, IAudioPlaybackService playback)
    {
        _recording = recording ?? throw new ArgumentNullException(nameof(recording));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));

        _inputVolume = _recording.InputVolume;
        _outputVolume = _playback.OutputVolume;

        _recording.InputVolumeChanged += RecordingOnInputVolumeChanged;
        _playback.OutputVolumeChanged += PlaybackOnOutputVolumeChanged;
        _recording.ErrorOccurred += (_, e) => ErrorOccurred?.Invoke(this, e);
        _playback.ErrorOccurred += (_, e) => ErrorOccurred?.Invoke(this, e);
    }

    public bool IsRecording => _recording.IsRecording;
    public bool IsPlaying => _playback.IsPlaying;

    public float InputVolume
    {
        get { lock (_gate) return _inputVolume; }
    }

    public float OutputVolume
    {
        get { lock (_gate) return _outputVolume; }
    }

    public int InputSampleRate => AudioFormat.StandardInputSampleRate;
    public int OutputSampleRate => AudioFormat.StandardOutputSampleRate;

    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    public Task StartRecordingAsync(Action<byte[]> onAudioCaptured) => _recording.StartRecordingAsync(onAudioCaptured);
    public Task StopRecordingAsync() => _recording.StopRecordingAsync();

    public Task PlayAudioAsync(byte[] pcmData) => _playback.PlayAudioAsync(pcmData);
    public Task StopPlaybackAsync() => _playback.StopPlaybackAsync();
    public Task InterruptPlaybackAsync() => _playback.InterruptPlaybackAsync();

    public void Dispose()
    {
        _recording.InputVolumeChanged -= RecordingOnInputVolumeChanged;
        _playback.OutputVolumeChanged -= PlaybackOnOutputVolumeChanged;

        _recording.Dispose();
        _playback.Dispose();
    }

    private void RecordingOnInputVolumeChanged(object? sender, float input)
    {
        lock (_gate)
        {
            _inputVolume = input;
        }
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(InputVolume, OutputVolume));
    }

    private void PlaybackOnOutputVolumeChanged(object? sender, float output)
    {
        lock (_gate)
        {
            _outputVolume = output;
        }
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(InputVolume, OutputVolume));
    }
}


