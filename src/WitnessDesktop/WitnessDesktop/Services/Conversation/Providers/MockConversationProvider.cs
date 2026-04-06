using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Conversation.Providers;

/// <summary>
/// Mock conversation provider for development and testing without API keys.
/// </summary>
/// <remarks>
/// Simulates a deterministic request/response provider for local development.
/// Contextual updates are recorded and incorporated into subsequent responses.
/// </remarks>
public sealed class MockConversationProvider : IConversationProvider
{
    private const int SimulatedConnectDelayMs = 250;
    private const int SimulatedReplyDelayMs = 120;

    private readonly object _gate = new();
    private readonly List<string> _contextUpdates = [];
    private readonly List<ChatMessage> _history = [];
    private CancellationTokenSource? _lifecycleCts;
    private CancellationTokenSource? _replyCts;
    private Agent? _connectedAgent;
    private bool _disposed;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? UserTranscriptReceived;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => State == ConnectionState.Connected;
    public bool SupportsVideo => false; // Mock does not process video
    public string ProviderName => "Mock Provider";

    public async Task ConnectAsync(Agent agent)
    {
        ThrowIfDisposed();

        CancellationToken token;
        lock (_gate)
        {
            if (State != ConnectionState.Disconnected) return;
            _lifecycleCts?.Cancel();
            _lifecycleCts?.Dispose();
            _lifecycleCts = new CancellationTokenSource();
            token = _lifecycleCts.Token;
            _connectedAgent = agent;
            _contextUpdates.Clear();
            _history.Clear();
            TransitionTo(ConnectionState.Connecting);
        }

        try
        {
            await Task.Delay(SimulatedConnectDelayMs, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed || token.IsCancellationRequested || State != ConnectionState.Connecting)
                return;

            TransitionTo(ConnectionState.Connected);
        }
    }

    public Task DisconnectAsync()
    {
        lock (_gate)
        {
            if (State == ConnectionState.Disconnected)
                return Task.CompletedTask;

            TransitionTo(ConnectionState.Disconnecting);
            CancelPendingWork();
            _connectedAgent = null;
            _contextUpdates.Clear();
            _history.Clear();
            TransitionTo(ConnectionState.Disconnected);
        }

        return Task.CompletedTask;
    }

    public Task SendAudioAsync(byte[] audioData)
    {
        ThrowIfDisposed();

        // Match real provider behavior: silently return when not connected
        if (!IsConnected)
            return Task.CompletedTask;

        CancellationTokenSource? replyToCancel = null;
        lock (_gate)
        {
            if (_replyCts is { IsCancellationRequested: false })
            {
                replyToCancel = _replyCts;
            }
        }

        if (replyToCancel is not null)
        {
            replyToCancel.Cancel();
            Interrupted?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg")
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            _contextUpdates.Add($"visual:{mimeType}");
            TrimContext();
        }

        return Task.CompletedTask;
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            await FailSendAsync("Cannot send text: mock provider is not connected.", throwException: true).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
            return;

        CancellationToken token;
        CancellationTokenSource replyCts;
        lock (_gate)
        {
            _replyCts?.Cancel();
            _replyCts?.Dispose();
            _replyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            replyCts = _replyCts;
            token = _replyCts.Token;
            _history.Add(new ChatMessage
            {
                Role = MessageRole.User,
                Intent = MessageIntent.GeneralChat,
                Content = text.Trim(),
                Source = ProviderName,
                DeliveryState = DeliveryState.Sent
            });
        }

        try
        {
            await Task.Delay(SimulatedReplyDelayMs, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_replyCts, replyCts))
                {
                    _replyCts.Dispose();
                    _replyCts = null;
                }
            }
        }

        ChatMessage reply;
        lock (_gate)
        {
            if (_disposed || State != ConnectionState.Connected || token.IsCancellationRequested)
                return;

            reply = CreateAssistantReply(text.Trim());
            _history.Add(reply);
        }

        EmitMessage(reply);
    }

    public Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(contextText))
            return Task.CompletedTask;

        lock (_gate)
        {
            _contextUpdates.Add(contextText.Trim());
            TrimContext();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelPendingWork();
    }

    private void CancelPendingWork()
    {
        try
        {
            _replyCts?.Cancel();
            _replyCts?.Dispose();
            _replyCts = null;
            _lifecycleCts?.Cancel();
            _lifecycleCts?.Dispose();
            _lifecycleCts = null;
        }
        catch
        {
            // Best effort for mock teardown.
        }
    }

    private void TransitionTo(ConnectionState newState)
    {
        State = newState;
        ConnectionStateChanged?.Invoke(this, newState);
    }

    private void EmitMessage(ChatMessage message)
    {
        MessageReceived?.Invoke(this, message);
        TextReceived?.Invoke(this, message.Content);
    }

    private ChatMessage CreateAssistantReply(string userText)
    {
        var latestContext = _contextUpdates.LastOrDefault();
        var lower = userText.ToLowerInvariant();

        string response;
        MessageIntent intent = MessageIntent.GeneralChat;

        if (lower.Contains("board") || lower.Contains("position") || lower.Contains("move"))
        {
            if (!string.IsNullOrWhiteSpace(latestContext))
            {
                response = $"Mock board read: using latest context '{Summarize(latestContext)}'. I would verify that line before committing.";
                intent = MessageIntent.LiveGameInfo;
            }
            else
            {
                response = "Mock board read: I do not have fresh board context yet. Ask me again after a capture update.";
                intent = MessageIntent.LiveGameInfo;
            }
        }
        else if (!string.IsNullOrWhiteSpace(latestContext))
        {
            response = $"Mock reply: I heard '{userText}'. Latest context is '{Summarize(latestContext)}'.";
        }
        else if (_connectedAgent is not null)
        {
            response = $"Mock reply from {_connectedAgent.Name}: I heard '{userText}'.";
        }
        else
        {
            response = $"Mock reply: I heard '{userText}'.";
        }

        return new ChatMessage
        {
            Role = MessageRole.Assistant,
            Intent = intent,
            Content = response,
            Source = ProviderName
        };
    }

    private static string Summarize(string text)
    {
        const int maxLength = 72;
        if (text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }

    private void TrimContext()
    {
        const int maxEntries = 8;
        while (_contextUpdates.Count > maxEntries)
            _contextUpdates.RemoveAt(0);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private Task FailSendAsync(string message, bool throwException)
    {
        ErrorOccurred?.Invoke(this, message);
        if (throwException)
            throw new InvalidOperationException(message);

        return Task.CompletedTask;
    }
}
