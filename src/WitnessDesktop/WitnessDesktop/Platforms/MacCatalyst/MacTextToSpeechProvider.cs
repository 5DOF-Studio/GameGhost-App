using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// Mac Catalyst TTS provider backed by GaimerSpeech.xcframework (AVSpeechSynthesizer.write()).
/// Returns 24kHz, 16-bit, mono PCM audio bytes — real synthesized speech, not a marker buffer.
///
/// Uses <see cref="IGaimerSpeechInterop"/> for native calls, enabling
/// unit testing on net8.0 without the actual native framework.
/// </summary>
public sealed class MacTextToSpeechProvider : ITextToSpeechProvider
{
    private readonly IGaimerSpeechInterop _interop;

    public MacTextToSpeechProvider(IGaimerSpeechInterop interop)
    {
        _interop = interop ?? throw new ArgumentNullException(nameof(interop));
    }

    public bool IsAvailable => _interop.IsTtsAvailable;
    public string EngineName => "Apple TTS";

    public async Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!IsAvailable)
        {
            System.Diagnostics.Debug.WriteLine("[MacTTS] TTS not available — returning null");
            return null;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[MacTTS] SynthesizeAsync: {text.Length} chars");
            var pcmBytes = await _interop.SynthesizeAsync(text, ct).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[MacTTS] Synthesized: {pcmBytes?.Length ?? 0} bytes PCM");
            return pcmBytes;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[MacTTS] SynthesizeAsync cancelled");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MacTTS] SynthesizeAsync error: {ex.Message}");
            return null;
        }
    }
}
