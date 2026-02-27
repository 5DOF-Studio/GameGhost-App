using AVFoundation;
using Foundation;
using WitnessDesktop.Services;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Ref-counted AVAudioSession manager for MacCatalyst (iOS audio stack).
/// </summary>
/// <remarks>
/// MacCatalyst requires AVAudioSession to be configured and activated BEFORE accessing AVAudioEngine input/output.
/// We keep the session active while either recording or playback holds a lease.
/// </remarks>
internal static class MacAudioSessionManager
{
    private static readonly object Gate = new();
    private static int _leaseCount;
    private static bool _configured;
    private static NSObject? _interruptionObserver;

    public static SessionLease Acquire(EventHandler<AudioErrorEventArgs>? errorSink)
    {
        lock (Gate)
        {
            _leaseCount++;
            if (_leaseCount == 1)
            {
                ConfigureAndActivate_NoLock(errorSink);
            }
            return new SessionLease(Release, errorSink);
        }
    }

    private static void Release(EventHandler<AudioErrorEventArgs>? errorSink)
    {
        lock (Gate)
        {
            if (_leaseCount <= 0) return;
            _leaseCount--;
            if (_leaseCount != 0) return;

            try
            {
                var audioSession = AVAudioSession.SharedInstance();
                audioSession.SetActive(false, out _);
                Console.WriteLine("[Audio][MacCatalyst] AVAudioSession deactivated (leaseCount=0)");
            }
            catch (Exception ex)
            {
                errorSink?.Invoke(null, new AudioErrorEventArgs("Failed to deactivate audio session.", ex, isFatal: false));
            }

            if (_interruptionObserver != null)
            {
                try { _interruptionObserver.Dispose(); } catch { /* best effort */ }
                _interruptionObserver = null;
            }

            _configured = false;
        }
    }

    private static void ConfigureAndActivate_NoLock(EventHandler<AudioErrorEventArgs>? errorSink)
    {
        if (_configured) return;

        try
        {
            var audioSession = AVAudioSession.SharedInstance();

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
            }
            else
            {
                Console.WriteLine("[Audio][MacCatalyst] AVAudioSession category set to PlayAndRecord");
            }

            // Low-latency hint (20ms)
            audioSession.SetPreferredIOBufferDuration(0.02, out NSError? bufferError);
            if (bufferError != null)
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetPreferredIOBufferDuration warning: {bufferError.LocalizedDescription}");

            // Keep existing behavior: prefer 16kHz (hint only; HW may ignore).
            audioSession.SetPreferredSampleRate(16000, out NSError? sampleRateError);
            if (sampleRateError != null)
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetPreferredSampleRate warning: {sampleRateError.LocalizedDescription}");

            var activated = audioSession.SetActive(true, out NSError? activationError);
            if (!activated || activationError != null)
            {
                var msg = $"Failed to activate audio session: {activationError?.LocalizedDescription ?? "unknown"}";
                Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession.SetActive(true) failed: {msg}");
                errorSink?.Invoke(null, new AudioErrorEventArgs(msg, activationError != null ? new NSErrorException(activationError) : null, isFatal: true));
                return;
            }

            Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession activated. HW sample rate: {audioSession.SampleRate}Hz");

            // Optional: keep a single observer for visibility (services handle engine restart themselves).
            _interruptionObserver = AVAudioSession.Notifications.ObserveInterruption((_, args) =>
            {
                Console.WriteLine($"[Audio][MacCatalyst] Audio interruption: {args.InterruptionType}");
            });

            _configured = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio][MacCatalyst] AVAudioSession setup exception: {ex}");
            errorSink?.Invoke(null, new AudioErrorEventArgs("Failed to configure audio session.", ex, isFatal: true));
        }
    }

    public sealed class SessionLease : IDisposable
    {
        private readonly Action<EventHandler<AudioErrorEventArgs>?> _release;
        private readonly EventHandler<AudioErrorEventArgs>? _errorSink;
        private bool _disposed;

        internal SessionLease(Action<EventHandler<AudioErrorEventArgs>?> release, EventHandler<AudioErrorEventArgs>? errorSink)
        {
            _release = release;
            _errorSink = errorSink;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _release(_errorSink);
        }
    }
}


