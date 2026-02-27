namespace WitnessDesktop.Services;

/// <summary>
/// Mock audio service for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// Simulates microphone input with random RMS levels and periodic "AI speaking" pulses.
/// Generates synthetic PCM16 white noise frames scaled by the simulated volume.
/// </para>
/// <para>
/// Used as a fallback on platforms without a real <see cref="IAudioService"/> implementation
/// (iOS, Android) or during UI development without hardware access.
/// </para>
/// </remarks>
public class MockAudioService : IAudioService
{
    private Timer? _volumeTimer;
    private readonly Random _random = new();
    private Action<byte[]>? _onAudioCaptured;
    private bool _disposed;

    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    public bool IsRecording { get; private set; }
    public bool IsPlaying { get; private set; }

    public float InputVolume { get; private set; }
    public float OutputVolume { get; private set; }

    public int InputSampleRate => 16000;
    public int OutputSampleRate => 24000;

    public Task StartRecordingAsync(Action<byte[]> onAudioCaptured)
    {
        if (IsRecording) return Task.CompletedTask;

        _onAudioCaptured = onAudioCaptured ?? throw new ArgumentNullException(nameof(onAudioCaptured));
        IsRecording = true;
        _volumeTimer = new Timer(SimulateVolume, null, 0, 100);
        return Task.CompletedTask;
    }

    public Task StopRecordingAsync()
    {
        IsRecording = false;
        _volumeTimer?.Dispose();
        _volumeTimer = null;
        InputVolume = 0f;
        RaiseVolumeChanged();
        return Task.CompletedTask;
    }

    public Task PlayAudioAsync(byte[] pcmData)
    {
        IsPlaying = true;
        Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                OutputVolume = (float)_random.NextDouble() * 0.8f + 0.2f;
                RaiseVolumeChanged();
                await Task.Delay(100);
            }
            IsPlaying = false;
            OutputVolume = 0f;
            RaiseVolumeChanged();
        });
        return Task.CompletedTask;
    }

    public Task StopPlaybackAsync()
    {
        IsPlaying = false;
        OutputVolume = 0f;
        RaiseVolumeChanged();
        return Task.CompletedTask;
    }

    public Task InterruptPlaybackAsync() => StopPlaybackAsync();

    private int _outputPulseCounter = 0;
    private bool _isOutputPulsing = false;

    private void SimulateVolume(object? state)
    {
        if (!IsRecording) return;
        
        var inputVolume = (float)(_random.NextDouble() * 0.6 + 0.1);
        InputVolume = inputVolume;
        RaiseVolumeChanged();
        // Emit a non-silent PCM16 mono frame so any RMS-based consumer doesn't flatline.
        // 20ms @ 16kHz => 320 samples => 640 bytes.
        try
        {
            _onAudioCaptured?.Invoke(GeneratePcm16NoiseFrame(inputVolume, sampleCount: 320));
        }
        catch (Exception ex)
        {
            // Callback must be fast; treat exceptions as non-fatal and keep recording.
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Audio capture callback threw an exception.", ex, isFatal: false));
        }

        _outputPulseCounter++;
        if (_outputPulseCounter >= 30 && !_isOutputPulsing && _random.NextDouble() > 0.7)
        {
            _isOutputPulsing = true;
            _outputPulseCounter = 0;
            Task.Run(SimulateAiSpeaking);
        }
    }

    private async Task SimulateAiSpeaking()
    {
        var duration = _random.Next(10, 25);
        for (int i = 0; i < duration && IsRecording; i++)
        {
            var volume = (float)(_random.NextDouble() * 0.7 + 0.2);
            OutputVolume = volume;
            RaiseVolumeChanged();
            await Task.Delay(100);
        }
        OutputVolume = 0f;
        RaiseVolumeChanged();
        _isOutputPulsing = false;
    }

    private void RaiseVolumeChanged()
    {
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(InputVolume, OutputVolume));
    }

    private byte[] GeneratePcm16NoiseFrame(float amplitude01, int sampleCount)
    {
        amplitude01 = Math.Clamp(amplitude01, 0f, 1f);
        var data = new byte[sampleCount * 2];

        for (var i = 0; i < sampleCount; i++)
        {
            // White noise in [-1, 1], scaled by amplitude.
            var n = ((float)_random.NextDouble() * 2f - 1f) * amplitude01;
            var sample = (short)Math.Clamp(n * short.MaxValue, short.MinValue, short.MaxValue);

            data[i * 2] = (byte)(sample & 0xFF);
            data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return data;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _volumeTimer?.Dispose();
        _volumeTimer = null;
        IsRecording = false;
        IsPlaying = false;
        _onAudioCaptured = null;
    }
}

