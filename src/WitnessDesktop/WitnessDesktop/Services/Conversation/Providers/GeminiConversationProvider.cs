using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Conversation.Providers;

/// <summary>
/// Adapter that wraps <see cref="GeminiLiveService"/> to implement <see cref="IConversationProvider"/>.
/// </summary>
public sealed class GeminiConversationProvider : IConversationProvider
{
    private readonly GeminiLiveService _geminiService;
    private readonly object _stateLock = new();
    private volatile ConnectionState _state = ConnectionState.Disconnected;
    private bool _disposed;

    public GeminiConversationProvider(IConfiguration configuration, string voice = "Fenrir")
    {
        _geminiService = new GeminiLiveService(configuration, voice);

        // Wire up event forwarding with thread-safe state dedup
        _geminiService.ConnectionStateChanged += (s, e) =>
        {
            lock (_stateLock)
            {
                if (_state == e) return;
                _state = e;
            }
            ConnectionStateChanged?.Invoke(this, e);
        };
        _geminiService.AudioReceived += (s, pcmData) =>
        {
            // Gemini 3.1 outputs 24kHz PCM per API docs.
            // Forward directly — matches AudioFormat.StandardOutputSampleRate.
            // If future testing reveals 16kHz output, add:
            //   var resampled = AudioResampler.Resample(pcmData, 16000, 24000);
            AudioReceived?.Invoke(this, pcmData);
        };
        _geminiService.TextReceived += (_, text) =>
        {
            TextReceived?.Invoke(this, text);
            MessageReceived?.Invoke(this, CreateAssistantMessage(text));
        };
        _geminiService.Interrupted += (s, e) => Interrupted?.Invoke(this, e);
        _geminiService.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(this, e);
        _geminiService.InputTranscriptionReceived += (_, transcript) =>
            UserTranscriptReceived?.Invoke(this, transcript);
    }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? UserTranscriptReceived;

    public ConnectionState State => _state;
    public bool IsConnected => _geminiService.IsConnected;
    public bool SupportsVideo => true; // Gemini supports image/video input
    public string ProviderName => "Gemini Live";

    public Task ConnectAsync(Agent agent) => _geminiService.ConnectAsync(agent);

    public async Task DisconnectAsync()
    {
        // Let GeminiLiveService manage state transitions — it fires ConnectionStateChanged
        // which our forwarding handler deduplicates. No need for adapter to fire its own events.
        await _geminiService.DisconnectAsync();
    }

    public Task SendAudioAsync(byte[] audioData) => _geminiService.SendAudioAsync(audioData);
    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg") => _geminiService.SendImageAsync(imageData, mimeType);
    public Task SendTextAsync(string text, CancellationToken cancellationToken = default) =>
        _geminiService.SendTextAsync(text, cancellationToken);

    public Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default) =>
        _geminiService.SendTextAsync($"[CONTEXT UPDATE] {contextText}", ct);

    /// <summary>
    /// Gemini Live is turn-based — sending text inherently triggers a response.
    /// Both methods behave identically for this provider.
    /// </summary>
    public Task SendContextualUpdateWithResponseAsync(string contextText, CancellationToken ct = default) =>
        _geminiService.SendTextAsync($"[CONTEXT UPDATE] {contextText}", ct);

    public async Task UpdateInstructionsAsync(string instructions)
    {
        if (!IsConnected) return;
        Console.WriteLine("[Gemini] UpdateInstructionsAsync: reconnecting with new instructions");
        await _geminiService.ReconnectWithInstructionsAsync(instructions);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _geminiService.Dispose();
    }

    private ChatMessage CreateAssistantMessage(string text) => new()
    {
        Role = MessageRole.Assistant,
        Intent = MessageIntent.GeneralChat,
        Content = text,
        Source = ProviderName
    };
}
