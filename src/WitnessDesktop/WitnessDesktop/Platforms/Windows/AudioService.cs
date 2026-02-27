using NAudio.Wave;
using WitnessDesktop.Services;

namespace WitnessDesktop.Platforms.Windows;

/// <summary>
/// Windows audio implementation using NAudio/WASAPI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Microphone:</b> Uses <see cref="WaveInEvent"/> for capture at 16kHz/16-bit/mono.
/// Most Windows audio devices support this format natively.
/// </para>
/// <para>
/// <b>Playback:</b> Uses <see cref="WaveOutEvent"/> with a bounded <see cref="BufferedWaveProvider"/>
/// (~500ms). Oldest samples are discarded on overflow to maintain low latency.
/// </para>
/// </remarks>
public sealed class AudioService : IAudioService
{
    private readonly object _gate = new();

    private WaveInEvent? _waveIn;
    private Action<byte[]>? _onAudioCaptured;

    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _playbackBuffer;

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
        if (onAudioCaptured is null) throw new ArgumentNullException(nameof(onAudioCaptured));

        lock (_gate)
        {
            ThrowIfDisposed();
            if (IsRecording) return Task.CompletedTask;

            _onAudioCaptured = onAudioCaptured;

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(InputSampleRate, 16, 1),
                BufferMilliseconds = 20
            };

            _waveIn.DataAvailable += OnInputDataAvailable;
            _waveIn.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null)
                    ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Recording stopped due to an error.", e.Exception, isFatal: false));
            };

            try
            {
                _waveIn.StartRecording();
                IsRecording = true;
            }
            catch (Exception ex)
            {
                CleanupWaveIn_NoLock();
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to start microphone recording.", ex, isFatal: true));
            }

            return Task.CompletedTask;
        }
    }

    public Task StopRecordingAsync()
    {
        lock (_gate)
        {
            if (!IsRecording) return Task.CompletedTask;

            try
            {
                _waveIn?.StopRecording();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to stop microphone recording cleanly.", ex, isFatal: false));
            }
            finally
            {
                CleanupWaveIn_NoLock();
                IsRecording = false;
                InputVolume = 0f;
                RaiseVolumeChanged_NoLock();
            }

            return Task.CompletedTask;
        }
    }

    public Task PlayAudioAsync(byte[] pcmData)
    {
        if (pcmData is null) throw new ArgumentNullException(nameof(pcmData));
        if (pcmData.Length == 0) return Task.CompletedTask;

        lock (_gate)
        {
            ThrowIfDisposed();

            EnsurePlaybackInitialized_NoLock();

            try
            {
                // Detect buffer overflow before adding (DiscardOnBufferOverflow silently drops oldest)
                int available = _playbackBuffer!.BufferLength - _playbackBuffer.BufferedBytes;
                if (pcmData.Length > available)
                {
                    // Throttle: only warn once per overflow burst
                    System.Diagnostics.Debug.WriteLine($"[Audio] Playback buffer overflow: {pcmData.Length} bytes requested, {available} available. Oldest audio will be dropped.");
                }

                _playbackBuffer.AddSamples(pcmData, 0, pcmData.Length);
                OutputVolume = CalculateRmsPcm16(pcmData);
                RaiseVolumeChanged_NoLock();

                if (!IsPlaying)
                {
                    _waveOut!.Play();
                    IsPlaying = true;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to play audio.", ex, isFatal: false));
            }

            return Task.CompletedTask;
        }
    }

    public Task StopPlaybackAsync()
    {
        lock (_gate)
        {
            StopPlayback_NoLock(clearBuffer: true);
            return Task.CompletedTask;
        }
    }

    public Task InterruptPlaybackAsync() => StopPlaybackAsync();

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            StopPlayback_NoLock(clearBuffer: true);
            CleanupWaveIn_NoLock();
        }
    }

    private void OnInputDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Called on a background thread by NAudio.
        byte[] frame;
        Action<byte[]>? callback;

        lock (_gate)
        {
            if (!IsRecording || _disposed) return;

            frame = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, frame, 0, e.BytesRecorded);

            InputVolume = CalculateRmsPcm16(frame);
            RaiseVolumeChanged_NoLock();

            callback = _onAudioCaptured;
        }

        if (callback is null) return;

        try
        {
            callback(frame);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Audio capture callback threw an exception.", ex, isFatal: false));
        }
    }

    private void EnsurePlaybackInitialized_NoLock()
    {
        if (_waveOut != null && _playbackBuffer != null) return;

        var format = new WaveFormat(OutputSampleRate, 16, 1);

        _playbackBuffer = new BufferedWaveProvider(format)
        {
            // ~500ms buffer at 24kHz mono 16-bit = 24000 samples/sec * 2 bytes * 0.5 sec = 24000 bytes
            BufferLength = 24000,
            DiscardOnBufferOverflow = true
        };

        _waveOut = new WaveOutEvent
        {
            DesiredLatency = 100
        };

        _waveOut.Init(_playbackBuffer);
        _waveOut.PlaybackStopped += (_, e) =>
        {
            if (e.Exception != null)
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Playback stopped due to an error.", e.Exception, isFatal: false));

            lock (_gate)
            {
                IsPlaying = false;
                OutputVolume = 0f;
                RaiseVolumeChanged_NoLock();
            }
        };
    }

    private void StopPlayback_NoLock(bool clearBuffer)
    {
        if (_waveOut is null) return;

        try
        {
            _waveOut.Stop();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to stop playback cleanly.", ex, isFatal: false));
        }

        if (clearBuffer)
        {
            try { _playbackBuffer?.ClearBuffer(); } catch { /* best effort */ }
        }

        IsPlaying = false;
        OutputVolume = 0f;
        RaiseVolumeChanged_NoLock();
    }

    private void CleanupWaveIn_NoLock()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnInputDataAvailable;
            try { _waveIn.Dispose(); } catch { /* best effort */ }
            _waveIn = null;
        }

        _onAudioCaptured = null;
    }

    private void RaiseVolumeChanged_NoLock()
    {
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(InputVolume, OutputVolume));
    }

    private static float CalculateRmsPcm16(byte[] buffer)
    {
        if (buffer.Length < 2) return 0f;

        double sum = 0;
        int samples = buffer.Length / 2;

        for (int i = 0; i < samples; i++)
        {
            int lo = buffer[i * 2];
            int hi = buffer[i * 2 + 1];
            short sample = (short)((hi << 8) | lo);
            float normalized = sample / 32768f;
            sum += normalized * normalized;
        }

        return (float)Math.Sqrt(sum / samples);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioService));
    }
}


