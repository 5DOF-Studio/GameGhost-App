using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Services.Conversation.Providers;

/// <summary>
/// Adapter that wraps <see cref="OpenAIRealtimeService"/> to implement <see cref="IConversationProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Audio Format Handling:</b><br/>
/// OpenAI Realtime API requires 24kHz audio input, but the app captures at 16kHz.
/// This provider automatically resamples input audio from 16kHz to 24kHz using
/// <see cref="AudioResampler"/> before sending to OpenAI.
/// </para>
/// <para>
/// <b>Output Audio:</b> OpenAI outputs 24kHz PCM, which matches the standard output format,
/// so no resampling is needed on the output path.
/// </para>
/// <para>
/// <b>Video/Image Support:</b> OpenAI Realtime API does not currently support image/video input,
/// so <see cref="SupportsVideo"/> returns false and <see cref="SendImageAsync"/> is a no-op.
/// </para>
/// </remarks>
public sealed class OpenAIConversationProvider : IConversationProvider
{
    private readonly OpenAIRealtimeService _openAiService;
    private bool _disposed;

    public OpenAIConversationProvider(IConfiguration configuration, string voice = "ash")
    {
        _openAiService = new OpenAIRealtimeService(configuration, voice);

        // Wire up event forwarding (also update local State)
        _openAiService.ConnectionStateChanged += (s, e) => { State = e; ConnectionStateChanged?.Invoke(this, e); };
        _openAiService.AudioReceived += (s, e) => AudioReceived?.Invoke(this, e);
        _openAiService.TextReceived += (_, text) =>
        {
            // Fire MessageReceived only — it's the structured path.
            // Firing both TextReceived + MessageReceived caused double display in MainViewModel.
            MessageReceived?.Invoke(this, CreateAssistantMessage(text));
        };
        _openAiService.Interrupted += (s, e) => Interrupted?.Invoke(this, e);
        _openAiService.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(this, e);
        _openAiService.InputTranscriptionReceived += (_, transcript) =>
            UserTranscriptReceived?.Invoke(this, transcript);
    }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? UserTranscriptReceived;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => _openAiService.IsConnected;
    
    /// <summary>
    /// OpenAI Realtime API does not currently support image/video input.
    /// </summary>
    public bool SupportsVideo => false;
    
    public string ProviderName => "OpenAI Realtime";

    public Task ConnectAsync(Agent agent) => _openAiService.ConnectAsync(agent);

    public async Task DisconnectAsync()
    {
        State = ConnectionState.Disconnecting;
        ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnecting);
        await _openAiService.DisconnectAsync();
        State = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnected);
    }
    
    /// <summary>
    /// Sends audio to OpenAI after resampling from 16kHz to 24kHz.
    /// </summary>
    /// <param name="audioData">PCM audio at 16kHz, 16-bit, mono.</param>
    public Task SendAudioAsync(byte[] audioData)
    {
        // OpenAI Realtime API requires 24kHz audio input
        // App microphone input is at 16kHz, so resample before sending
        var resampledAudio = AudioResampler.Resample(
            audioData, 
            AudioFormat.StandardInputSampleRate,  // 16kHz
            AudioFormat.StandardOutputSampleRate  // 24kHz
        );
        
        return _openAiService.SendAudioAsync(resampledAudio);
    }
    
    /// <summary>
    /// OpenAI Realtime API does not support image input. This is a no-op.
    /// </summary>
    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg")
    {
        // OpenAI Realtime does not support image input - silently ignore
        // Log for debugging purposes
        System.Diagnostics.Debug.WriteLine("[OpenAI] SendImageAsync called but OpenAI Realtime does not support images");
        return Task.CompletedTask;
    }

    public Task SendTextAsync(string text, CancellationToken cancellationToken = default) =>
        _openAiService.SendTextAsync(text, requestResponse: true, cancellationToken: cancellationToken);

    public Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default) =>
        _openAiService.SendTextAsync($"[CONTEXT UPDATE] {contextText}", requestResponse: false, cancellationToken: ct);

    public Task UpdateInstructionsAsync(string instructions) =>
        _openAiService.UpdateInstructionsAsync(instructions);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _openAiService.Dispose();
    }

    private ChatMessage CreateAssistantMessage(string text) => new()
    {
        Role = MessageRole.Assistant,
        Intent = MessageIntent.GeneralChat,
        Content = text,
        Source = ProviderName
    };
}
