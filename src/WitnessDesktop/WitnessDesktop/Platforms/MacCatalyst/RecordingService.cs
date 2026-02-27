using System.Runtime.InteropServices;
using AVFoundation;
using Foundation;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// MacCatalyst microphone capture implementation using a dedicated AVAudioEngine.
/// </summary>
public sealed class RecordingService : IAudioRecordingService
{
    private readonly object _gate = new();
    private bool _disposed;

    private AVAudioEngine? _engine;
    private NSObject? _interruptionObserver;
    private MacAudioSessionManager.SessionLease? _sessionLease;

    private Action<byte[]>? _onAudioCaptured;
    private long _capturedFrames;

    public bool IsRecording { get; private set; }
    public float InputVolume { get; private set; }

    public event EventHandler<float>? InputVolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    public async Task StartRecordingAsync(Action<byte[]> onAudioCaptured)
    {
        if (onAudioCaptured is null) throw new ArgumentNullException(nameof(onAudioCaptured));

        Console.WriteLine("[Audio][MacCatalyst][Recording] StartRecordingAsync requested");

        // Avoid forcing background continuations; AVAudio* setup is more stable when started on the calling thread.
        bool permitted = await EnsureMicPermissionAsync();
        if (!permitted)
        {
            Console.WriteLine("[Audio][MacCatalyst][Recording] Microphone permission denied");
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Microphone permission denied.", null, isFatal: true));
            return;
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (IsRecording) return;

            _onAudioCaptured = onAudioCaptured;
            _capturedFrames = 0;

            _sessionLease ??= MacAudioSessionManager.Acquire((_, e) => ErrorOccurred?.Invoke(this, e));

            EnsureEngineInitialized_NoLock();
            var inputNode = _engine!.InputNode;
            var tapFormat = inputNode.GetBusOutputFormat(0);

            if (tapFormat?.SampleRate < 1 || tapFormat?.ChannelCount < 1)
            {
                Console.WriteLine("[Audio][MacCatalyst][Recording] ERROR: Invalid input format after session activation.");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs(
                    "No valid microphone input format available. Please check microphone permissions and hardware.",
                    null,
                    isFatal: true));
                return;
            }

            uint bufferFrames = (uint)(tapFormat!.SampleRate * 0.02); // ~20ms at native rate
            Console.WriteLine($"[Audio][MacCatalyst][Recording] Installing input tap (frames={bufferFrames}, nativeFormat={tapFormat.CommonFormat}/{tapFormat.SampleRate:0}Hz/ch={tapFormat.ChannelCount})...");

            try
            {
                inputNode.InstallTapOnBus(0, bufferFrames, tapFormat, (buffer, _) =>
                {
                    var pcm16_16k = ConvertMicBufferToPcm16_16k_Mono(buffer);
                    if (pcm16_16k.Length == 0) return;

                    Action<byte[]>? cb;
                    float rms;
                    var frameIndex = Interlocked.Increment(ref _capturedFrames);

                    lock (_gate)
                    {
                        if (!IsRecording || _disposed) return;
                        cb = _onAudioCaptured;
                        rms = CalculateRmsPcm16(pcm16_16k);
                        InputVolume = rms;
                    }

                    InputVolumeChanged?.Invoke(this, InputVolume);

                    try
                    {
                        if (frameIndex <= 3 || frameIndex % 100 == 0)
                            Console.WriteLine($"[Audio][MacCatalyst][Recording] Captured frame #{frameIndex}: {pcm16_16k.Length} bytes, rms={rms:0.000}");
                        cb?.Invoke(pcm16_16k);
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Audio capture callback threw an exception.", ex, isFatal: false));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio][MacCatalyst][Recording] InstallTapOnBus failed: {ex}");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to install microphone tap.", ex, isFatal: true));
                return;
            }

            if (!StartEngine_NoLock(out var startError))
            {
                try { inputNode.RemoveTapOnBus(0); } catch { /* best effort */ }
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to start audio engine for recording.", startError, isFatal: true));
                return;
            }

            IsRecording = true;
            Console.WriteLine("[Audio][MacCatalyst][Recording] Recording started");
        }
    }

    public Task StopRecordingAsync()
    {
        lock (_gate)
        {
            if (!IsRecording) return Task.CompletedTask;

            try { _engine?.InputNode.RemoveTapOnBus(0); } catch { /* best effort */ }
            try { _engine?.Stop(); } catch { /* best effort */ }

            IsRecording = false;
            _onAudioCaptured = null;
            InputVolume = 0f;
        }

        InputVolumeChanged?.Invoke(this, 0f);

        lock (_gate)
        {
            // Release session if we are fully stopped (recording-only service).
            _sessionLease?.Dispose();
            _sessionLease = null;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try { StopRecordingAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }

        lock (_gate)
        {
            if (_interruptionObserver != null)
            {
                try { _interruptionObserver.Dispose(); } catch { /* best effort */ }
                _interruptionObserver = null;
            }

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

        // Force node creation
        _ = _engine.InputNode;

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
                if (_engine is null || !IsRecording) return;
                try
                {
                    // Ensure session is active again; engine may be paused by the system.
                    AVAudioSession.SharedInstance().SetActive(true, out NSError? sessionError);
                    if (sessionError != null)
                        throw new NSErrorException(sessionError);
                    NSError? startError = null;
                    if (!_engine.Running)
                        _engine.StartAndReturnError(out startError);
                    // If StartAndReturnError reports an error, surface it.
                    if (!_engine.Running && startError != null)
                        throw new NSErrorException(startError);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to resume recording after interruption.", ex, isFatal: false));
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

    private static byte[] ConvertMicBufferToPcm16_16k_Mono(AVAudioPcmBuffer input)
    {
        // Most commonly: PCMFloat32, non-interleaved. We'll normalize to PCM16 @ 16kHz mono.
        if (input.FrameLength == 0) return Array.Empty<byte>();

        var inputFormat = input.Format;
        var nativeRate = (int)Math.Round(inputFormat.SampleRate);
        if (nativeRate <= 0) return Array.Empty<byte>();

        // If already PCMInt16 mono interleaved, we can extract and just resample if needed.
        byte[] pcm16Native;
        if (inputFormat.CommonFormat == AVAudioCommonFormat.PCMInt16 && inputFormat.Interleaved)
        {
            pcm16Native = ExtractPcmBytes(input);
        }
        else if (inputFormat.CommonFormat == AVAudioCommonFormat.PCMFloat32)
        {
            pcm16Native = FloatBufferToPcm16(input);
        }
        else
        {
            // Unexpected format; best-effort fall back to raw extraction (may be empty).
            pcm16Native = ExtractPcmBytes(input);
        }

        if (pcm16Native.Length == 0) return Array.Empty<byte>();

        // Downmix if input delivered interleaved stereo PCMInt16
        if (inputFormat.CommonFormat == AVAudioCommonFormat.PCMInt16 && inputFormat.Interleaved && inputFormat.ChannelCount == 2)
            pcm16Native = AudioResampler.StereoToMono(pcm16Native);

        // Resample to 16kHz (PCM16 mono)
        return nativeRate == AudioFormat.StandardInputSampleRate
            ? pcm16Native
            : AudioResampler.ResampleToStandardInput(pcm16Native, nativeRate);
    }

    private static byte[] FloatBufferToPcm16(AVAudioPcmBuffer input)
    {
        var format = input.Format;
        var frameLength = (int)input.FrameLength;
        var channels = (int)format.ChannelCount;
        if (frameLength <= 0 || channels <= 0) return Array.Empty<byte>();

        var channelDataPtr = input.FloatChannelData;
        if (channelDataPtr == IntPtr.Zero) return Array.Empty<byte>();

        // Read channel pointers
        var channelPtrs = new IntPtr[channels];
        for (int c = 0; c < channels; c++)
        {
            channelPtrs[c] = Marshal.ReadIntPtr(channelDataPtr, c * IntPtr.Size);
            if (channelPtrs[c] == IntPtr.Zero) return Array.Empty<byte>();
        }

        var output = new byte[frameLength * 2]; // mono PCM16
        for (int i = 0; i < frameLength; i++)
        {
            float mono = 0f;
            for (int c = 0; c < channels; c++)
            {
                mono += Marshal.PtrToStructure<float>(channelPtrs[c] + i * sizeof(float));
            }
            mono /= channels;

            int intSample = (int)(mono * 32767f);
            intSample = Math.Clamp(intSample, short.MinValue, short.MaxValue);
            short s = (short)intSample;

            output[i * 2] = (byte)(s & 0xFF);
            output[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }

        return output;
    }

    private static byte[] ExtractPcmBytes(AVAudioPcmBuffer buffer)
    {
        var list = buffer.AudioBufferList;
        if (list.Count == 0) return Array.Empty<byte>();
        var audioBuffer = list[0];
        var len = (int)audioBuffer.DataByteSize;
        if (len <= 0 || audioBuffer.Data == IntPtr.Zero) return Array.Empty<byte>();
        var data = new byte[len];
        Marshal.Copy(audioBuffer.Data, data, 0, len);
        return data;
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

    private static Task<bool> EnsureMicPermissionAsync()
    {
        var status = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
        Console.WriteLine($"[Audio][MacCatalyst][Recording] Mic authorization status: {status}");
        if (status == AVAuthorizationStatus.Authorized) return Task.FromResult(true);
        if (status == AVAuthorizationStatus.Denied || status == AVAuthorizationStatus.Restricted) return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();
        AVCaptureDevice.RequestAccessForMediaType(AVAuthorizationMediaType.Audio, granted =>
        {
            Console.WriteLine($"[Audio][MacCatalyst][Recording] Mic permission prompt result: granted={granted}");
            tcs.TrySetResult(granted);
        });
        return tcs.Task;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RecordingService));
    }
}


