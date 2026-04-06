using System.Runtime.InteropServices;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Production implementation that calls into GaimerSpeech.xcframework via P/Invoke.
/// </summary>
public sealed class GaimerSpeechInterop : IGaimerSpeechInterop
{
    private bool? _sttAvailable;
    private bool? _ttsAvailable;

    public bool IsSttAvailable
    {
        get
        {
            try
            {
                _sttAvailable ??= GaimerSpeechNativeMethods.speech_is_stt_available();
                return _sttAvailable.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GaimerSpeechInterop] STT availability check failed: {ex.Message}");
                _sttAvailable = false;
                return false;
            }
        }
    }

    public bool IsTtsAvailable
    {
        get
        {
            try
            {
                _ttsAvailable ??= GaimerSpeechNativeMethods.speech_is_tts_available();
                return _ttsAvailable.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GaimerSpeechInterop] TTS availability check failed: {ex.Message}");
                _ttsAvailable = false;
                return false;
            }
        }
    }

    public async Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
    {
        if (pcmAudio == null || pcmAudio.Length == 0)
            return null;

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        // Pin the callback delegate to prevent GC collection during native call
        GaimerSpeechNativeMethods.TranscribeCallback callback = (transcriptPtr, transcriptLength) =>
        {
            if (transcriptPtr == IntPtr.Zero || transcriptLength <= 0)
            {
                tcs.TrySetResult(null);
                return;
            }

            try
            {
                var transcript = Marshal.PtrToStringUTF8(transcriptPtr);
                tcs.TrySetResult(transcript);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        var callbackHandle = GCHandle.Alloc(callback);
        try
        {
            // Pin the PCM data and pass to native
            var pcmHandle = GCHandle.Alloc(pcmAudio, GCHandleType.Pinned);
            try
            {
                GaimerSpeechNativeMethods.speech_transcribe(
                    pcmHandle.AddrOfPinnedObject(),
                    pcmAudio.Length,
                    16000, // Standard STT input rate
                    callback);
            }
            finally
            {
                pcmHandle.Free();
            }

            // Wait for callback with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), ct);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
            {
                tcs.TrySetResult(null);
                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            callbackHandle.Free();
        }
    }

    public async Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var tcs = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        // Pin the callback delegate to prevent GC collection during native call
        GaimerSpeechNativeMethods.SynthesizeCallback callback = (pcmPtr, pcmLength) =>
        {
            if (pcmPtr == IntPtr.Zero || pcmLength <= 0)
            {
                tcs.TrySetResult(null);
                return;
            }

            try
            {
                // Copy the unmanaged buffer into managed byte array
                var pcmBytes = new byte[pcmLength];
                Marshal.Copy(pcmPtr, pcmBytes, 0, pcmLength);

                // Free the native buffer
                GaimerSpeechNativeMethods.speech_free_buffer(pcmPtr);

                tcs.TrySetResult(pcmBytes);
            }
            catch (Exception ex)
            {
                // Still try to free native buffer on error
                try { GaimerSpeechNativeMethods.speech_free_buffer(pcmPtr); }
                catch { /* best effort */ }
                tcs.TrySetException(ex);
            }
        };

        var callbackHandle = GCHandle.Alloc(callback);
        try
        {
            GaimerSpeechNativeMethods.speech_synthesize(text, callback);

            // Wait for callback with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), ct);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
            {
                tcs.TrySetResult(null);
                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            callbackHandle.Free();
        }
    }
}
