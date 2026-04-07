using WitnessDesktop.Models;
using WitnessDesktop.Services.Local;

namespace WitnessDesktop.Services.Conversation.Providers;

/// <summary>
/// Adapter that exposes a local MiniCPM-backed audio client as an IConversationProvider.
/// This keeps local voice behind the same contract as cloud providers.
/// </summary>
public sealed class LocalMiniCpmConversationProvider : IConversationProvider
{
    private readonly ILocalAudioConversationClient _client;
    private bool _disposed;

    public LocalMiniCpmConversationProvider(ILocalAudioConversationClient client)
    {
        _client = client;

        _client.ConnectionStateChanged += (_, state) =>
        {
            State = state;
            ConnectionStateChanged?.Invoke(this, state);
        };
        _client.AudioReceived += (_, audio) => AudioReceived?.Invoke(this, audio);
        _client.TextReceived += (_, text) =>
        {
            TextReceived?.Invoke(this, text);
            MessageReceived?.Invoke(this, CreateAssistantMessage(text));
        };
        _client.Interrupted += (_, _) => Interrupted?.Invoke(this, EventArgs.Empty);
        _client.ErrorOccurred += (_, message) => ErrorOccurred?.Invoke(this, message);
    }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? UserTranscriptReceived;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => _client.IsConnected;
    public bool SupportsVideo => false;
    public string ProviderName => $"Local MiniCPM ({_client.RuntimeName})";

    public Task ConnectAsync(Agent agent) => _client.ConnectAsync(agent);

    public Task DisconnectAsync() => _client.DisconnectAsync();

    public Task SendAudioAsync(byte[] audioData) => _client.SendAudioAsync(audioData);

    public Task SendTextAsync(string text, CancellationToken cancellationToken = default) =>
        _client.SendTextAsync(text, cancellationToken);

    public Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default) =>
        _client.SendContextualUpdateAsync(contextText, ct);

    /// <summary>
    /// Local provider — context update with response behaves identically to regular context update.
    /// </summary>
    public Task SendContextualUpdateWithResponseAsync(string contextText, CancellationToken ct = default) =>
        _client.SendContextualUpdateAsync(contextText, ct);

    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg")
    {
        System.Diagnostics.Debug.WriteLine("[LocalMiniCPM] SendImageAsync called on voice provider; ignoring image input.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
    }

    private ChatMessage CreateAssistantMessage(string text) => new()
    {
        Role = MessageRole.Assistant,
        Intent = MessageIntent.GeneralChat,
        Content = text,
        Source = ProviderName
    };
}
