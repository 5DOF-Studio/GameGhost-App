using Microsoft.Extensions.Configuration;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Conversation.Providers;

/// <summary>
/// Adapter that wraps <see cref="GeminiLiveService"/> to implement <see cref="IConversationProvider"/>.
/// </summary>
/// <remarks>
/// This adapter exists to:
/// <list type="bullet">
/// <item>Protect the working <see cref="GeminiLiveService"/> from modifications.</item>
/// <item>Provide a consistent interface for the conversation provider abstraction.</item>
/// <item>Enable future enhancements without touching core Gemini logic.</item>
/// </list>
/// </remarks>
public sealed class GeminiConversationProvider : IConversationProvider
{
    private readonly GeminiLiveService _geminiService;
    private bool _disposed;

    public GeminiConversationProvider(IConfiguration configuration)
    {
        _geminiService = new GeminiLiveService(configuration);

        // Wire up event forwarding
        _geminiService.ConnectionStateChanged += (s, e) => ConnectionStateChanged?.Invoke(this, e);
        _geminiService.AudioReceived += (s, e) => AudioReceived?.Invoke(this, e);
        _geminiService.TextReceived += (s, e) => TextReceived?.Invoke(this, e);
        _geminiService.Interrupted += (s, e) => Interrupted?.Invoke(this, e);
        _geminiService.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(this, e);
    }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;

    public ConnectionState State => _geminiService.State;
    public bool IsConnected => _geminiService.IsConnected;
    public bool SupportsVideo => true; // Gemini supports image/video input
    public string ProviderName => "Gemini Live";

    public Task ConnectAsync(Agent agent) => _geminiService.ConnectAsync(agent);

    public async Task DisconnectAsync()
    {
        ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnecting);
        await _geminiService.DisconnectAsync();
    }
    public Task SendAudioAsync(byte[] audioData) => _geminiService.SendAudioAsync(audioData);
    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg") => _geminiService.SendImageAsync(imageData, mimeType);
    public Task SendTextAsync(string text, CancellationToken cancellationToken = default) =>
        _geminiService.SendTextAsync(text, cancellationToken);

    public Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default) =>
        _geminiService.SendTextAsync($"[CONTEXT UPDATE] {contextText}", ct);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _geminiService.Dispose();
    }
}

