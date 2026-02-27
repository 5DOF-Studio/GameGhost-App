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
    /// Raised when the AI's response is interrupted (user spoke during playback).
    /// </summary>
    event EventHandler? Interrupted;

    /// <summary>
    /// Raised when an error occurs. Message contains user-friendly description.
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

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
    /// </summary>
    /// <param name="contextText">Formatted context string.</param>
    /// <param name="ct">Optional cancellation token.</param>
    Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default);
}

