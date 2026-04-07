using WitnessDesktop.Models;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Services.Conversation;

/// <summary>
/// Provider-agnostic interface for real-time voice conversation with AI.
/// Implementations wrap multimodal backends (Gemini, OpenAI) that support text + audio + images/video.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design Decision:</b> Only multimodal providers are supported to enable full visual coaching
/// where the AI can see game screenshots and provide context-aware guidance. Audio-only providers
/// (e.g., ElevenLabs) are excluded as they cannot fulfill the visual requirement.
/// </para>
/// <para>
/// <b>Audio Format Contract:</b><br/>
/// All implementations MUST use the standard formats defined in <see cref="AudioFormat"/>:
/// </para>
/// <list type="bullet">
/// <item><b>Input:</b> <see cref="AudioFormat.StandardInputSampleRate"/> (16kHz), 16-bit, mono PCM</item>
/// <item><b>Output:</b> <see cref="AudioFormat.StandardOutputSampleRate"/> (24kHz), 16-bit, mono PCM</item>
/// </list>
/// <para>
/// If the underlying AI provider uses a different audio format, the implementation
/// MUST convert to/from the standard format. Use <see cref="AudioResampler"/> for
/// sample rate conversion.
/// </para>
/// <para>
/// Implementations must be thread-safe. Events may fire on background threads;
/// consumers should marshal UI updates to the main thread.
/// </para>
/// </remarks>
public interface IConversationProvider : IDisposable
{
    /// <summary>
    /// Raised when connection state changes.
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Raised when audio data is received from the AI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>IMPORTANT:</b> Audio data MUST be in the standard output format:
    /// <see cref="AudioFormat.StandardOutputSampleRate"/> (24kHz), 16-bit, mono PCM.
    /// </para>
    /// <para>
    /// If the underlying provider outputs a different format, use <see cref="AudioResampler"/>
    /// to convert before raising this event.
    /// </para>
    /// </remarks>
    event EventHandler<byte[]>? AudioReceived;

    /// <summary>
    /// Raised when text/transcript is received from the AI.
    /// </summary>
    event EventHandler<string>? TextReceived;

    /// <summary>
    /// Raised when a structured message is received from the AI.
    /// Legacy consumers may continue using <see cref="TextReceived"/>.
    /// </summary>
    event EventHandler<ChatMessage>? MessageReceived;

    /// <summary>
    /// Raised when the AI's response is interrupted (user spoke during playback).
    /// </summary>
    event EventHandler? Interrupted;

    /// <summary>
    /// Raised when an error occurs. Message contains user-friendly description.
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Raised when a finalized user speech transcript is available.
    /// Fired after the provider's STT produces a complete utterance.
    /// Not all providers support this (Gemini does not currently).
    /// </summary>
    event EventHandler<string>? UserTranscriptReceived;

    /// <summary>
    /// Connects to the AI provider with the specified agent's system instruction.
    /// </summary>
    Task ConnectAsync(Agent agent);

    /// <summary>
    /// Disconnects from the provider gracefully.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Sends audio data to the provider.
    /// </summary>
    /// <remarks>
    /// Audio data MUST be in the standard input format:
    /// <see cref="AudioFormat.StandardInputSampleRate"/> (16kHz), 16-bit, mono PCM.
    /// </remarks>
    /// <param name="audioData">PCM audio bytes in standard input format.</param>
    Task SendAudioAsync(byte[] audioData);

    /// <summary>
    /// Sends user text to the provider. Only call when <see cref="IsConnected"/> is true.
    /// </summary>
    /// <param name="text">User message text.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Completed task on success; throws or surfaces via ErrorOccurred on failure.</returns>
    Task SendTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends image data to the provider (if supported).
    /// Check <see cref="SupportsVideo"/> before calling.
    /// </summary>
    Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg");

    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// True when State == Connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// True if this provider supports video/image input.
    /// </summary>
    bool SupportsVideo { get; }

    /// <summary>
    /// Human-readable name of the provider (e.g., "Gemini Live", "OpenAI Realtime").
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Sends a contextual update to the provider (e.g., brain hint for voice synthesis).
    /// Used by BrainEventRouter to inject live analysis into voice conversation.
    /// The AI receives the context but is NOT prompted to generate a response.
    /// </summary>
    /// <param name="contextText">Formatted context string.</param>
    /// <param name="ct">Optional cancellation token.</param>
    Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default);

    /// <summary>
    /// Sends a contextual update AND prompts the AI to generate a spoken response.
    /// Used for deferred answers and urgent alerts where the voice must speak
    /// without waiting for the user to talk first.
    /// </summary>
    /// <param name="contextText">Formatted context string.</param>
    /// <param name="ct">Optional cancellation token.</param>
    Task SendContextualUpdateWithResponseAsync(string contextText, CancellationToken ct = default);

    /// <summary>
    /// Update the provider's system instructions at runtime (e.g., on game state change).
    /// Used to inform the voice provider about InGame/OutGame context.
    /// No-op on providers that don't support runtime instruction updates.
    /// </summary>
    Task UpdateInstructionsAsync(string instructions) => Task.CompletedTask;
}
