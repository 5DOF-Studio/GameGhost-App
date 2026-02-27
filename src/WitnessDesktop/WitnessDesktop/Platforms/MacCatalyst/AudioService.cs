using System.Runtime.InteropServices;
using AVFoundation;
using Foundation;
using WitnessDesktop.Services;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Mac Catalyst audio implementation using AVAudioEngine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Microphone:</b> Installs a tap on <see cref="AVAudioInputNode"/> at 16kHz/16-bit/mono.
/// Requires <c>NSMicrophoneUsageDescription</c> in Info.plist and
/// <c>com.apple.security.device.audio-input</c> entitlement.
/// </para>
/// <para>
/// <b>Playback:</b> Uses <see cref="AVAudioPlayerNode"/> attached to the engine's main mixer.
/// Audio is scheduled as PCM buffers at 24kHz/16-bit/mono.
/// </para>
/// <para>
/// <b>IMPORTANT (MacCatalyst):</b> AVAudioSession must be configured and activated BEFORE
/// accessing AVAudioEngine's input node. MacCatalyst uses the iOS audio stack which requires
/// explicit session management. Without this, inputNode.GetBusOutputFormat(0) returns invalid
/// format (sampleRate=0, channelCount=0).
/// </para>
/// </remarks>
public sealed class AudioService : IAudioService
{
    private readonly object _gate = new();

    private AVAudioEngine? _engine;
    private AVAudioPlayerNode? _playerNode;
    private NSObject? _interruptionObserver;
    private bool _audioSessionConfigured;

    private Action<byte[]>? _onAudioCaptured;
    private bool _disposed;
    private long _capturedFrames;
    private (double SampleRate, uint Channels, AVAudioCommonFormat CommonFormat, bool Interleaved)? _micInputFormatKey;
    private AVAudioConverter? _micConverter;

    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;

    public bool IsRecording { get; private set; }
    public bool IsPlaying { get; private set; }

    public float InputVolume { get; private set; }
    public float OutputVolume { get; private set; }

    public int InputSampleRate => 16000;
    public int OutputSampleRate => 24000;

    public async Task StartRecordingAsync(Action<byte[]> onAudioCaptured)
    {
        if (onAudioCaptured is null) throw new ArgumentNullException(nameof(onAudioCaptured));

        Console.WriteLine("[Audio][MacCatalyst] StartRecordingAsync requested");
        // IMPORTANT (MacCatalyst): avoid forcing a background continuation here.
        // AVAudioEngine setup/taps can behave poorly when invoked from background continuations.
        bool permitted = await EnsureMicPermissionAsync();
        if (!permitted)
        {
            Console.WriteLine("[Audio][MacCatalyst] Microphone permission denied");
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Microphone permission denied.", null, isFatal: true));
            return;
        }
        Console.WriteLine("[Audio][MacCatalyst] Microphone permission granted");

        lock (_gate)
        {
            ThrowIfDisposed();
            if (IsRecording) return;

            _onAudioCaptured = onAudioCaptured;
            _capturedFrames = 0;
            Console.WriteLine("[Audio][MacCatalyst] About to initialize AVAudioEngine...");
            EnsureEngineInitialized_NoLock();
            Console.WriteLine("[Audio][MacCatalyst] AVAudioEngine initialized");

            var inputNode = _engine!.InputNode;
            // IMPORTANT: do NOT force the hardware to 16kHz PCM16 here. Some devices/hosts reject it with:
            // "Input HW format is invalid" (seen in run_maccatalyst.log).
            // Instead, tap the native input format and convert to PCM16/16k/mono in the callback.
            var tapFormat = inputNode.GetBusOutputFormat(0);

            // Validate format before proceeding - if AVAudioSession wasn't properly activated,
            // the format will be invalid (sampleRate=0, channelCount=0)
            if (tapFormat?.SampleRate < 1 || tapFormat?.ChannelCount < 1)
            {
                Console.WriteLine($"[Audio][MacCatalyst] ERROR: Invalid input format even after session activation! " +
                    $"Format: {tapFormat?.CommonFormat}/{tapFormat?.SampleRate:0}Hz/ch={tapFormat?.ChannelCount}");
                Console.WriteLine("[Audio][MacCatalyst] This usually means AVAudioSession wasn't properly activated or no microphone is available.");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs(
                    "No valid microphone input format available. Please check microphone permissions and hardware.",
                    null,
                    isFatal: true));
                return;
            }

            // 20ms @ 16kHz => 320 frames. Adjust buffer size based on actual sample rate.
            // tapFormat is guaranteed non-null here due to validation above
            uint bufferFrames = (uint)(tapFormat!.SampleRate * 0.02); // 20ms worth of frames
            Console.WriteLine($"[Audio][MacCatalyst] Installing input tap (bus=0, frames={bufferFrames}, nativeFormat={tapFormat.CommonFormat}/{tapFormat.SampleRate:0}Hz/ch={tapFormat.ChannelCount}/interleaved={tapFormat.Interleaved})...");
            try
            {
                inputNode.InstallTapOnBus(0, bufferFrames, tapFormat, (buffer, _) =>
                {
                    var pcm = ConvertMicBufferToPcm16_16k_Mono(buffer);
                    if (pcm.Length == 0)
                        return;

                    Action<byte[]>? cb;
                    float rms;
                    var frameIndex = Interlocked.Increment(ref _capturedFrames);
                    lock (_gate)
                    {
                        if (!IsRecording || _disposed) return;
                        rms = CalculateRmsPcm16(pcm);
                        InputVolume = rms;
                        RaiseVolumeChanged_NoLock();
                        cb = _onAudioCaptured;
                    }

                    try
                    {
                        // Avoid log spam: print the first few frames + then once every ~2s (100 frames @ 20ms).
                        if (frameIndex <= 5 || frameIndex % 100 == 0)
                            Console.WriteLine($"[Audio][MacCatalyst] Captured frame #{frameIndex}: {pcm.Length} bytes, rms={rms:0.000}");
                        cb?.Invoke(pcm);
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Audio capture callback threw an exception.", ex, isFatal: false));
                    }
                });
                Console.WriteLine("[Audio][MacCatalyst] Input tap installed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio][MacCatalyst] InstallTapOnBus failed: {ex}");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to install microphone tap.", ex, isFatal: true));
                return;
            }

            Console.WriteLine("[Audio][MacCatalyst] Tap installed. Starting AVAudioEngine...");
            var ok = StartEngine_NoLock(out var startError);
            if (!ok)
            {
                try { inputNode.RemoveTapOnBus(0); } catch { /* best effort */ }
                Console.WriteLine($"[Audio][MacCatalyst] Failed to start audio engine for recording: {startError}");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to start audio engine for recording.", startError, isFatal: true));
                return;
            }
            Console.WriteLine("[Audio][MacCatalyst] AVAudioEngine started");

            IsRecording = true;
            Console.WriteLine("[Audio][MacCatalyst] Recording started");
        }
    }

    private byte[] ConvertMicBufferToPcm16_16k_Mono(AVAudioPcmBuffer input)
    {
        // Fast path: already in the desired format (PCMInt16/16k/mono/interleaved).
        if (input.Format.CommonFormat == AVAudioCommonFormat.PCMInt16 &&
            Math.Abs(input.Format.SampleRate - InputSampleRate) < 0.5 &&
            input.Format.ChannelCount == 1 &&
            input.Format.Interleaved)
        {
            return ExtractPcmData(input);
        }

        // Manual conversion: PCMFloat32 (any sample rate) -> PCMInt16 @ 16kHz mono
        // AVAudioConverter has issues with non-interleaved float32 to interleaved int16
        return ConvertFloat32ToPcm16_Manual(input);
    }

    /// <summary>
    /// Manually converts PCMFloat32 audio to PCMInt16 @ 16kHz mono.
    /// Handles resampling by simple decimation (takes every Nth sample).
    /// </summary>
    private byte[] ConvertFloat32ToPcm16_Manual(AVAudioPcmBuffer input)
    {
        if (input.Format.CommonFormat != AVAudioCommonFormat.PCMFloat32)
        {
            Console.WriteLine($"[Audio][MacCatalyst] Unexpected format: {input.Format.CommonFormat}, expected PCMFloat32");
            return Array.Empty<byte>();
        }

        var inputSampleRate = input.Format.SampleRate;
        var frameLength = (int)input.FrameLength;

        if (frameLength == 0)
            return Array.Empty<byte>();

        // Get float data pointer - for non-interleaved mono, use FloatChannelData[0]
        var channelDataPtr = input.FloatChannelData;
        if (channelDataPtr == IntPtr.Zero)
        {
            Console.WriteLine("[Audio][MacCatalyst] FloatChannelData is null");
            return Array.Empty<byte>();
        }

        // FloatChannelData is float** - read pointer to channel 0's float array
        IntPtr channel0Ptr = Marshal.ReadIntPtr(channelDataPtr);
        if (channel0Ptr == IntPtr.Zero)
        {
            Console.WriteLine("[Audio][MacCatalyst] Channel 0 data pointer is null");
            return Array.Empty<byte>();
        }

        // Copy float samples to managed array
        var floatSamples = new float[frameLength];
        Marshal.Copy(channel0Ptr, floatSamples, 0, frameLength);

        // Calculate decimation ratio (e.g., 48000 / 16000 = 3)
        int decimationRatio = (int)Math.Round(inputSampleRate / InputSampleRate);
        if (decimationRatio < 1) decimationRatio = 1;

        int outputFrames = frameLength / decimationRatio;
        if (outputFrames == 0)
            return Array.Empty<byte>();

        // Output buffer: 2 bytes per sample (int16)
        var output = new byte[outputFrames * 2];

        for (int i = 0; i < outputFrames; i++)
        {
            int srcIndex = i * decimationRatio;
            if (srcIndex >= frameLength) break;

            // Convert float [-1.0, 1.0] to int16 [-32768, 32767]
            float sample = floatSamples[srcIndex];
            int intSample = (int)(sample * 32767f);
            
            // Clamp
            if (intSample > 32767) intSample = 32767;
            else if (intSample < -32768) intSample = -32768;

            short shortSample = (short)intSample;

            // Little-endian
            output[i * 2] = (byte)(shortSample & 0xFF);
            output[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
        }

        return output;
    }

    public Task StopRecordingAsync()
    {
        lock (_gate)
        {
            if (!IsRecording) return Task.CompletedTask;

            try
            {
                _engine?.InputNode.RemoveTapOnBus(0);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to remove microphone tap cleanly.", ex, isFatal: false));
            }

            IsRecording = false;
            _onAudioCaptured = null;
            InputVolume = 0f;
            RaiseVolumeChanged_NoLock();

            // Stop the engine only if we're not actively playing.
            if (!IsPlaying)
            {
                try { _engine?.Stop(); } catch { /* best effort */ }
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
            EnsureEngineInitialized_NoLock();
            EnsurePlayerInitialized_NoLock();

            if (_playerNode is null)
            {
                Console.WriteLine("[Audio][MacCatalyst] Player node not available, skipping playback");
                return Task.CompletedTask;
            }

            if (!StartEngine_NoLock(out var startError))
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to start audio engine for playback.", startError, isFatal: true));
                return Task.CompletedTask;
            }

            try
            {
                // Get the format the player node is connected with (mixer's format)
                var mixerFormat = _engine!.MainMixerNode.GetBusOutputFormat(0);
                
                // Convert our 24kHz PCM16 mono to the mixer's native format
                var convertedBuffer = ConvertPcm16ToMixerFormat(pcmData, mixerFormat);
                if (convertedBuffer is null)
                {
                    Console.WriteLine("[Audio][MacCatalyst] Failed to convert audio for playback");
                    return Task.CompletedTask;
                }

                OutputVolume = CalculateRmsPcm16(pcmData);
                RaiseVolumeChanged_NoLock();

                IsPlaying = true;
                _playerNode.ScheduleBuffer(convertedBuffer, () =>
                {
                    lock (_gate)
                    {
                        // Best-effort: when a scheduled buffer finishes, drop the meter if no more audio is queued.
                        OutputVolume = 0f;
                        RaiseVolumeChanged_NoLock();
                        IsPlaying = false;
                    }
                });

                if (!_playerNode.Playing)
                    _playerNode.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio][MacCatalyst] Playback error: {ex.Message}");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to schedule/play audio buffer.", ex, isFatal: false));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Converts PCM16 24kHz mono audio to the mixer's native format for playback.
    /// </summary>
    private AVAudioPcmBuffer? ConvertPcm16ToMixerFormat(byte[] pcm16Data, AVAudioFormat mixerFormat)
    {
        try
        {
            int inputFrames = pcm16Data.Length / 2; // 2 bytes per sample for PCM16
            if (inputFrames == 0) return null;

            // Calculate output frames based on sample rate ratio
            double sampleRateRatio = mixerFormat.SampleRate / OutputSampleRate; // e.g., 48000/24000 = 2
            int outputFrames = (int)(inputFrames * sampleRateRatio);

            // Create output buffer in mixer's format
            var outputBuffer = new AVAudioPcmBuffer(mixerFormat, (uint)outputFrames)
            {
                FrameLength = (uint)outputFrames
            };

            // Convert PCM16 to float samples first
            var floatSamples = new float[inputFrames];
            for (int i = 0; i < inputFrames; i++)
            {
                short sample = (short)(pcm16Data[i * 2] | (pcm16Data[i * 2 + 1] << 8));
                floatSamples[i] = sample / 32768f;
            }

            // Get pointer to output buffer's float channel data
            var channelDataPtr = outputBuffer.FloatChannelData;
            if (channelDataPtr == IntPtr.Zero)
            {
                Console.WriteLine("[Audio][MacCatalyst] Output buffer FloatChannelData is null");
                return null;
            }

            // Get channel 0 pointer
            IntPtr channel0Ptr = Marshal.ReadIntPtr(channelDataPtr);
            if (channel0Ptr == IntPtr.Zero)
            {
                Console.WriteLine("[Audio][MacCatalyst] Output channel 0 pointer is null");
                return null;
            }

            // Upsample using linear interpolation
            var outputFloats = new float[outputFrames];
            for (int i = 0; i < outputFrames; i++)
            {
                double srcIndex = i / sampleRateRatio;
                int srcIndexInt = (int)srcIndex;
                double fraction = srcIndex - srcIndexInt;

                if (srcIndexInt >= inputFrames - 1)
                {
                    outputFloats[i] = floatSamples[inputFrames - 1];
                }
                else
                {
                    // Linear interpolation
                    outputFloats[i] = (float)(floatSamples[srcIndexInt] * (1 - fraction) +
                                             floatSamples[srcIndexInt + 1] * fraction);
                }
            }

            // Copy to output buffer
            Marshal.Copy(outputFloats, 0, channel0Ptr, outputFrames);

            // If stereo, copy same data to channel 1
            if (mixerFormat.ChannelCount > 1)
            {
                IntPtr channel1Ptr = Marshal.ReadIntPtr(channelDataPtr, IntPtr.Size);
                if (channel1Ptr != IntPtr.Zero)
                {
                    Marshal.Copy(outputFloats, 0, channel1Ptr, outputFrames);
                }
            }

            return outputBuffer;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio][MacCatalyst] ConvertPcm16ToMixerFormat error: {ex.Message}");
            return null;
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

            StopPlayback_NoLock();

            try { _engine?.InputNode.RemoveTapOnBus(0); } catch { /* best effort */ }
            try { _engine?.Stop(); } catch { /* best effort */ }
            _engine?.Dispose();
            _engine = null;

            _playerNode?.Dispose();
            _playerNode = null;

            _micConverter?.Dispose();
            _micConverter = null;

            // Remove interruption observer
            if (_interruptionObserver != null)
            {
                _interruptionObserver.Dispose();
                _interruptionObserver = null;
            }

            // Deactivate audio session
            if (_audioSessionConfigured)
            {
                try
                {
                    var audioSession = AVAudioSession.SharedInstance();
                    audioSession.SetActive(false, out _);
                    Console.WriteLine("[Audio][MacCatalyst] AVAudioSession deactivated");
                }
                catch { /* best effort */ }
                _audioSessionConfigured = false;
            }

            _onAudioCaptured = null;
        }
    }

    private void EnsureEngineInitialized_NoLock()
    {
        if (_engine != null) return;

        // CRITICAL: Configure and activate AVAudioSession BEFORE creating/using AVAudioEngine.
        // MacCatalyst uses the iOS audio stack which requires explicit session management.
        // Without this, inputNode.GetBusOutputFormat(0) returns invalid format (sampleRate=0).
        EnsureAudioSessionConfigured_NoLock();

        try
        {
            Console.WriteLine("[Audio][MacCatalyst] Creating AVAudioEngine...");
            _engine = new AVAudioEngine();
            Console.WriteLine("[Audio][MacCatalyst] AVAudioEngine created");

            // NOTE: Do NOT call _engine.Prepare() here!
            // On MacCatalyst, Prepare() throws "inputNode != nullptr || outputNode != nullptr"
            // if we haven't accessed any nodes yet. The engine will auto-prepare when started.
            // We just need to access the InputNode to force its creation:
            var inputNode = _engine.InputNode;
            Console.WriteLine($"[Audio][MacCatalyst] InputNode accessed. Format: {inputNode.GetBusOutputFormat(0).SampleRate}Hz");

            // IMPORTANT: Set up the player node NOW, before the engine starts.
            // If we try to attach/connect nodes after the engine is running, we get
            // kAudioUnitErr_FormatNotSupported (-10868) errors due to format conflicts.
            SetupPlayerNode_NoLock();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio][MacCatalyst] ERROR during AVAudioEngine init: {ex}");
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to initialize AVAudioEngine.", ex, isFatal: true));
            try { _engine?.Dispose(); } catch { /* best effort */ }
            _engine = null;
        }
    }

    /// <summary>
    /// Sets up the player node for audio playback. Must be called before engine starts.
    /// </summary>
    private void SetupPlayerNode_NoLock()
    {
        if (_engine is null || _playerNode != null) return;

        try
        {
            Console.WriteLine("[Audio][MacCatalyst] Setting up player node...");
            _playerNode = new AVAudioPlayerNode();
            _engine.AttachNode(_playerNode);

            // Use the mixer's output format to ensure compatibility.
            // The mixer can handle format conversion from our 24kHz to the hardware rate.
            var mixerFormat = _engine.MainMixerNode.GetBusOutputFormat(0);
            Console.WriteLine($"[Audio][MacCatalyst] Mixer output format: {mixerFormat.SampleRate}Hz, {mixerFormat.ChannelCount}ch, {mixerFormat.CommonFormat}");

            // Connect player to mixer using a format the mixer can accept.
            // We'll use the mixer's native format and convert our audio before scheduling.
            _engine.Connect(_playerNode, _engine.MainMixerNode, mixerFormat);
            Console.WriteLine("[Audio][MacCatalyst] Player node connected to mixer");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio][MacCatalyst] Failed to set up player node: {ex.Message}");
            // Don't throw - recording can still work without playback
            try { _playerNode?.Dispose(); } catch { }
            _playerNode = null;
        }
    }

    /// <summary>
    /// Configures and activates AVAudioSession for recording and playback.
    /// This MUST be called before accessing AVAudioEngine's input node on MacCatalyst.
    /// </summary>
    private void EnsureAudioSessionConfigured_NoLock()
    {
        if (_audioSessionConfigured) return;

        try
        {
            var audioSession = AVAudioSession.SharedInstance();

            // Use PlayAndRecord for mic + speaker, with options for default speaker routing and Bluetooth
            var categoryOptions = AVAudioSessionCategoryOptions.DefaultToSpeaker |
                                  AVAudioSessionCategoryOptions.AllowBluetooth |
                                  AVAudioSessionCategoryOptions.AllowBluetoothA2DP;

            var categorySet = audioSession.SetCategory(
                AVAudioSessionCategory.PlayAndRecord,
                AVAudioSessionMode.Default,
                categoryOptions,
                out NSError? categoryError);

            if (!categorySet || categoryError != null)
            {
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetCategory failed: {categoryError?.LocalizedDescription ?? "unknown error"}");
                // Don't return - try to continue anyway
            }
            else
            {
                Console.WriteLine("[Audio][MacCatalyst] AVAudioSession category set to PlayAndRecord");
            }

            // Set preferred sample rate to 16kHz (Gemini's expected input format)
            // This is a hint to the system; actual rate may differ
            audioSession.SetPreferredSampleRate(16000, out NSError? sampleRateError);
            if (sampleRateError != null)
            {
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetPreferredSampleRate warning: {sampleRateError.LocalizedDescription}");
            }

            // Set preferred buffer duration for low latency (20ms = 0.02s)
            audioSession.SetPreferredIOBufferDuration(0.02, out NSError? bufferError);
            if (bufferError != null)
            {
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetPreferredIOBufferDuration warning: {bufferError.LocalizedDescription}");
            }

            // ACTIVATE the session - this is REQUIRED for inputNode to have a valid format
            var activated = audioSession.SetActive(true, out NSError? activationError);
            if (!activated || activationError != null)
            {
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetActive(true) failed: {activationError?.LocalizedDescription ?? "unknown error"}");
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs(
                    $"Failed to activate audio session: {activationError?.LocalizedDescription ?? "unknown"}",
                    activationError != null ? new NSErrorException(activationError) : null,
                    isFatal: true));
                return;
            }

            Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession activated. HW sample rate: {audioSession.SampleRate}Hz");
            Console.WriteLine("[Audio][MacCatalyst] AVAudioSession activation complete (continuing)");

            // Subscribe to audio interruption notifications (phone calls, Siri, etc.)
            SetupInterruptionObserver();

            _audioSessionConfigured = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession setup exception: {ex}");
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs("Failed to configure audio session.", ex, isFatal: true));
        }
    }

    /// <summary>
    /// Sets up observer for audio interruption notifications to handle system interruptions gracefully.
    /// </summary>
    private void SetupInterruptionObserver()
    {
        _interruptionObserver = AVAudioSession.Notifications.ObserveInterruption((sender, args) =>
        {
            var interruptionType = args.InterruptionType;
            Console.WriteLine($"[Audio][MacCatalyst] Audio interruption: {interruptionType}");

            if (interruptionType == AVAudioSessionInterruptionType.Began)
            {
                // System interrupted our audio (e.g., phone call, Siri)
                // The engine will be paused automatically
                Console.WriteLine("[Audio][MacCatalyst] Audio interrupted by system - engine paused");
            }
            else if (interruptionType == AVAudioSessionInterruptionType.Ended)
            {
                // Interruption ended - try to resume if we should be recording/playing
                Console.WriteLine("[Audio][MacCatalyst] Audio interruption ended");

                // Check if we should resume
                var options = args.Option;
                if (options.HasFlag(AVAudioSessionInterruptionOptions.ShouldResume))
                {
                    lock (_gate)
                    {
                        if (_engine != null && (IsRecording || IsPlaying))
                        {
                            try
                            {
                                var audioSession = AVAudioSession.SharedInstance();
                                audioSession.SetActive(true, out _);

                                if (!_engine.Running)
                                {
                                    _engine.StartAndReturnError(out _);
                                    Console.WriteLine("[Audio][MacCatalyst] Audio engine resumed after interruption");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Audio][MacCatalyst] Failed to resume after interruption: {ex.Message}");
                            }
                        }
                    }
                }
            }
        });
    }

    private void EnsurePlayerInitialized_NoLock()
    {
        if (_engine is null) throw new InvalidOperationException("Engine must be initialized first.");
        
        // Player should already be set up during engine initialization.
        // If not (unlikely), try to set it up now - may fail if engine is running.
        if (_playerNode is null)
        {
            Console.WriteLine("[Audio][MacCatalyst] WARNING: Player node not set up during init, attempting late setup...");
            SetupPlayerNode_NoLock();
        }
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
        if (!IsPlaying && _playerNode is null) return;

        try { _playerNode?.Stop(); } catch { /* best effort */ }

        IsPlaying = false;
        OutputVolume = 0f;
        RaiseVolumeChanged_NoLock();

        // Stop the engine only if we're not actively recording.
        if (!IsRecording)
        {
            try { _engine?.Stop(); } catch { /* best effort */ }
        }
    }

    private void RaiseVolumeChanged_NoLock()
    {
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(InputVolume, OutputVolume));
    }

    private static byte[] ExtractPcmData(AVAudioPcmBuffer buffer)
    {
        var audioBufferList = buffer.AudioBufferList;
        if (audioBufferList.Count == 0) return Array.Empty<byte>();

        var audioBuffer = audioBufferList[0];
        var dataLength = (int)audioBuffer.DataByteSize;
        if (dataLength <= 0) return Array.Empty<byte>();

        var data = new byte[dataLength];
        Marshal.Copy(audioBuffer.Data, data, 0, dataLength);
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
        Console.WriteLine($"[Audio][MacCatalyst] Mic authorization status: {status}");
        if (status == AVAuthorizationStatus.Authorized) return Task.FromResult(true);
        if (status == AVAuthorizationStatus.Denied || status == AVAuthorizationStatus.Restricted) return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();
        AVCaptureDevice.RequestAccessForMediaType(AVAuthorizationMediaType.Audio, granted =>
        {
            Console.WriteLine($"[Audio][MacCatalyst] Mic permission prompt result: granted={granted}");
            tcs.TrySetResult(granted);
        });
        return tcs.Task;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioService));
    }
}


