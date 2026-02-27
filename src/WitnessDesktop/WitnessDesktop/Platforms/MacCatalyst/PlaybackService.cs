using System.Runtime.InteropServices;
using AVFoundation;
using Foundation;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// MacCatalyst playback implementation using a dedicated AVAudioEngine.
/// Converts 24kHz PCM16 input to the mixer's native format before playback.
/// </summary>
public sealed class PlaybackService : IAudioPlaybackService
{
    private readonly object _gate = new();
    private bool _disposed;

    private AVAudioEngine? _engine;
    private AVAudioPlayerNode? _playerNode;
    private AVAudioFormat? _mixerFormat;
    private NSObject? _interruptionObserver;
    private MacAudioSessionManager.SessionLease? _sessionLease;

    private int _pendingBuffers;

    public bool IsPlaying { get; private set; }
    public float OutputVolume { get; private set; }

    public event EventHandler<float>? OutputVolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    public Task PlayAudioAsync(byte[] pcmData)
    {
        if (pcmData is null) throw new ArgumentNullException(nameof(pcmData));
        if (pcmData.Length == 0) return Task.CompletedTask;

        lock (_gate)
        {
            ThrowIfDisposed();

            _sessionLease ??= MacAudioSessionManager.Acquire((_, e) => ErrorOccurred?.Invoke(this, e));
            EnsureEngineInitialized_NoLock();

            if (!StartEngine_NoLock(out var startError))
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to start audio engine for playback.", startError, isFatal: true));
                return Task.CompletedTask;
            }

            try
            {
                // Convert 24kHz PCM16 to mixer's native format
                var buffer = ConvertToMixerFormat(pcmData);
                if (buffer is null) return Task.CompletedTask;

                OutputVolume = CalculateRmsPcm16(pcmData);
                OutputVolumeChanged?.Invoke(this, OutputVolume);

                IsPlaying = true;
                _pendingBuffers++;

                _playerNode!.ScheduleBuffer(buffer, () =>
                {
                    int remaining;
                    lock (_gate)
                    {
                        remaining = Math.Max(0, _pendingBuffers - 1);
                        _pendingBuffers = remaining;
                        if (remaining == 0)
                        {
                            IsPlaying = false;
                            OutputVolume = 0f;
                        }
                    }

                    if (remaining == 0)
                        OutputVolumeChanged?.Invoke(this, 0f);
                });

                if (!_playerNode!.Playing)
                    _playerNode.Play();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to schedule/play audio buffer.", ex, isFatal: false));
            }

            return Task.CompletedTask;
        }
    }

    public Task StopPlaybackAsync()
    {
        lock (_gate)
        {
            StopPlayback_NoLock();
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
        }

        try { StopPlaybackAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }

        lock (_gate)
        {
            if (_interruptionObserver != null)
            {
                try { _interruptionObserver.Dispose(); } catch { /* best effort */ }
                _interruptionObserver = null;
            }

            _mixerFormat = null;

            _playerNode?.Dispose();
            _playerNode = null;

            _engine?.Dispose();
            _engine = null;

            _sessionLease?.Dispose();
            _sessionLease = null;
        }
    }

    private void EnsureEngineInitialized_NoLock()
    {
        if (_engine != null) return;

        _engine = new AVAudioEngine();
        _playerNode = new AVAudioPlayerNode();
        _engine.AttachNode(_playerNode);

        // Get the mixer's native format (typically 48kHz Float32 stereo)
        _mixerFormat = _engine.MainMixerNode.GetBusOutputFormat(0);
        Console.WriteLine($"[Audio][MacCatalyst][Playback] Mixer format: {_mixerFormat.SampleRate}Hz, {_mixerFormat.ChannelCount}ch");

        // Connect player node with the mixer's native format
        _engine.Connect(_playerNode, _engine.MainMixerNode, _mixerFormat);
        Console.WriteLine("[Audio][MacCatalyst][Playback] Player node connected to mixer");

        SetupInterruptionObserver_NoLock();
    }

    private void SetupInterruptionObserver_NoLock()
    {
        _interruptionObserver = AVAudioSession.Notifications.ObserveInterruption((_, args) =>
        {
            if (args.InterruptionType != AVAudioSessionInterruptionType.Ended) return;
            if (!args.Option.HasFlag(AVAudioSessionInterruptionOptions.ShouldResume)) return;

            lock (_gate)
            {
                if (_engine is null || (!IsPlaying && _pendingBuffers == 0)) return;
                try
                {
                    AVAudioSession.SharedInstance().SetActive(true, out NSError? sessionError);
                    if (sessionError != null)
                        throw new NSErrorException(sessionError);
                    NSError? startError = null;
                    if (!_engine.Running)
                        _engine.StartAndReturnError(out startError);
                    if (!_engine.Running && startError != null)
                        throw new NSErrorException(startError);
                    if (_playerNode != null && !_playerNode.Playing)
                        _playerNode.Play();
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to resume playback after interruption.", ex, isFatal: false));
                }
            }
        });
    }

    private bool StartEngine_NoLock(out Exception? error)
    {
        error = null;
        if (_engine is null) return false;
        if (_engine.Running) return true;
        try
        {
            var ok = _engine.StartAndReturnError(out NSError? nsError);
            if (!ok && nsError != null)
                error = new NSErrorException(nsError);
            return ok;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private void StopPlayback_NoLock()
    {
        if (_playerNode is null && !IsPlaying) return;

        try
        {
            _playerNode?.Stop();
            _playerNode?.Reset();
        }
        catch { /* best effort */ }

        _pendingBuffers = 0;
        IsPlaying = false;
        OutputVolume = 0f;

        try { _engine?.Stop(); } catch { /* best effort */ }

        OutputVolumeChanged?.Invoke(this, 0f);

        // Release the audio session lease when playback stops.
        _sessionLease?.Dispose();
        _sessionLease = null;
    }

    private static AVAudioFormat CreatePlaybackFormat()
        => new(AVAudioCommonFormat.PCMInt16, AudioFormat.StandardOutputSampleRate, AudioFormat.Channels, interleaved: true);

    /// <summary>
    /// Converts 24kHz PCM16 mono input to the mixer's native format (typically 48kHz Float32 stereo).
    /// Uses manual resampling since AVAudioConverter has API issues in .NET MAUI.
    /// </summary>
    private AVAudioPcmBuffer? ConvertToMixerFormat(byte[] pcm16Data)
    {
        if (_mixerFormat is null) return null;

        try
        {
            int inputSampleRate = (int)AudioFormat.StandardOutputSampleRate; // 24000
            int outputSampleRate = (int)_mixerFormat.SampleRate;
            int outputChannels = (int)_mixerFormat.ChannelCount;
            
            int inputSamples = pcm16Data.Length / 2; // mono PCM16 = 2 bytes per sample
            if (inputSamples <= 0) return null;

            // Calculate output samples
            int outputSamples = (int)((long)inputSamples * outputSampleRate / inputSampleRate);
            
            // Create output buffer in mixer's format
            var outputBuffer = new AVAudioPcmBuffer(_mixerFormat, (uint)outputSamples);
            outputBuffer.FrameLength = (uint)outputSamples;

            // Get float data pointer (non-interleaved for standard mixer format)
            var floatChannelData = outputBuffer.FloatChannelData;
            if (floatChannelData == IntPtr.Zero) return null;

            // Read channel pointers from the pointer-to-pointer
            IntPtr leftChannelPtr = Marshal.ReadIntPtr(floatChannelData, 0);
            IntPtr rightChannelPtr = outputChannels > 1 ? Marshal.ReadIntPtr(floatChannelData, IntPtr.Size) : IntPtr.Zero;

            // Create temporary array for resampled float samples
            var resampledFloats = new float[outputSamples];

            // Resample and convert PCM16 → Float32
            for (int outIdx = 0; outIdx < outputSamples; outIdx++)
            {
                // Linear interpolation resampling
                double srcPos = (double)outIdx * inputSampleRate / outputSampleRate;
                int srcIdx = (int)srcPos;
                double frac = srcPos - srcIdx;

                // Clamp to valid range
                srcIdx = Math.Min(srcIdx, inputSamples - 1);
                int nextIdx = Math.Min(srcIdx + 1, inputSamples - 1);

                // Read PCM16 samples
                short s0 = (short)(pcm16Data[srcIdx * 2] | (pcm16Data[srcIdx * 2 + 1] << 8));
                short s1 = (short)(pcm16Data[nextIdx * 2] | (pcm16Data[nextIdx * 2 + 1] << 8));

                // Interpolate and convert to float [-1, 1]
                resampledFloats[outIdx] = (float)((s0 + (s1 - s0) * frac) / 32768.0);
            }

            // Copy to left channel
            Marshal.Copy(resampledFloats, 0, leftChannelPtr, outputSamples);

            // Copy to right channel if stereo (mono → stereo duplication)
            if (rightChannelPtr != IntPtr.Zero)
                Marshal.Copy(resampledFloats, 0, rightChannelPtr, outputSamples);

            return outputBuffer;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio][MacCatalyst][Playback] ConvertToMixerFormat exception: {ex.Message}");
            return null;
        }
    }

    private static AVAudioPcmBuffer? CreatePcm16Buffer(AVAudioFormat format, byte[] pcm16Data)
    {
        try
        {
            int frames = pcm16Data.Length / 2; // mono PCM16
            if (frames <= 0) return null;

            var buffer = new AVAudioPcmBuffer(format, (uint)frames)
            {
                FrameLength = (uint)frames
            };

            var list = buffer.AudioBufferList;
            if (list.Count == 0) return null;
            var audioBuffer = list[0];
            if (audioBuffer.Data == IntPtr.Zero) return null;

            // Ensure we don't overrun (DataByteSize should be >= pcm16Data.Length).
            var max = (int)audioBuffer.DataByteSize;
            var len = Math.Min(max, pcm16Data.Length);
            Marshal.Copy(pcm16Data, 0, audioBuffer.Data, len);
            return buffer;
        }
        catch
        {
            return null;
        }
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
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackService));
    }
}


