namespace WitnessDesktop.Services.Local;

/// <summary>
/// Stub TTS provider that reports unavailable. Used on platforms without
/// native speech synthesis or when TTS is not yet implemented.
/// </summary>
public sealed class StubTextToSpeechProvider : ITextToSpeechProvider
{
    public bool IsAvailable => false;
    public string EngineName => "None";

    public Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        return Task.FromResult<byte[]?>(null);
    }
}
