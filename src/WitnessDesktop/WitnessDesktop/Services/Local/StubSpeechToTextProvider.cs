namespace WitnessDesktop.Services.Local;

/// <summary>
/// Stub STT provider that reports unavailable. Used on platforms without
/// native speech recognition or when STT is not yet implemented.
/// </summary>
public sealed class StubSpeechToTextProvider : ISpeechToTextProvider
{
    public bool IsAvailable => false;
    public string EngineName => "None";

    public Task<string?> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }
}
