using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Mac Catalyst STT provider backed by GaimerSpeech.xcframework (SFSpeechRecognizer).
/// Accepts 16kHz, 16-bit, mono PCM audio and returns a transcript string.
///
/// Uses <see cref="IGaimerSpeechInterop"/> for native calls, enabling
/// unit testing on net8.0 without the actual native framework.
/// </summary>
public sealed class MacSpeechToTextProvider : ISpeechToTextProvider
{
    private readonly IGaimerSpeechInterop _interop;

    public MacSpeechToTextProvider(IGaimerSpeechInterop interop)
    {
        _interop = interop ?? throw new ArgumentNullException(nameof(interop));
    }

    public bool IsAvailable => _interop.IsSttAvailable;
    public string EngineName => "Apple Speech";

    public async Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
    {
        if (pcmAudio == null || pcmAudio.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine("[MacSTT] TranscribeAsync called with null/empty audio");
            return null;
        }

        if (!IsAvailable)
        {
            System.Diagnostics.Debug.WriteLine("[MacSTT] STT not available — returning null");
            return null;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[MacSTT] TranscribeAsync: {pcmAudio.Length} bytes");
            var transcript = await _interop.TranscribeAsync(pcmAudio, ct).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[MacSTT] Transcript: '{transcript ?? "<null>"}'");
            return transcript;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[MacSTT] TranscribeAsync cancelled");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MacSTT] TranscribeAsync error: {ex.Message}");
            return null;
        }
    }
}
