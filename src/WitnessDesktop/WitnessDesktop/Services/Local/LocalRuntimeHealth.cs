namespace WitnessDesktop.Services.Local;

/// <summary>
/// Health snapshot from a local model runtime.
/// </summary>
public sealed class LocalRuntimeHealth
{
    /// <summary>Whether the runtime process/service is reachable.</summary>
    public bool RuntimeAvailable { get; init; }

    /// <summary>Whether the runtime can serve brain (vision) inference.</summary>
    public bool BrainAvailable { get; init; }

    /// <summary>Whether the runtime can serve voice inference.</summary>
    public bool VoiceAvailable { get; init; }

    /// <summary>Name of the runtime (e.g. "ollama", "direct").</summary>
    public string? RuntimeName { get; init; }

    /// <summary>Model identifier loaded in the runtime.</summary>
    public string? ModelId { get; init; }

    /// <summary>Reason the runtime is unhealthy, if applicable.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Whether platform speech-to-text input is available for local voice.</summary>
    public bool SpeechInputAvailable { get; init; }

    /// <summary>Whether platform text-to-speech output is available for local voice.</summary>
    public bool SpeechOutputAvailable { get; init; }

    /// <summary>Name of the STT engine, if any (e.g. "Apple Speech", "None").</summary>
    public string? SttEngineName { get; init; }

    /// <summary>Name of the TTS engine, if any (e.g. "Apple TTS", "None").</summary>
    public string? TtsEngineName { get; init; }
}
