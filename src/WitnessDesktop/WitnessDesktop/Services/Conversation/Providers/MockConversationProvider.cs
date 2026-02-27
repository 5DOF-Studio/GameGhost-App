using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Conversation.Providers;

/// <summary>
/// Mock conversation provider for development and testing without API keys.
/// </summary>
/// <remarks>
/// Simulates AI responses with random text messages at intervals.
/// Does not produce audio output.
/// </remarks>
public sealed class MockConversationProvider : IConversationProvider
{
    private static readonly string[] MockResponses =
    [
        "Nice move! I see you're developing your pieces well.",
        "That's a solid strategy. Keep the pressure on!",
        "Interesting position. Have you considered the knight fork?",
        "GG! You're playing really well today.",
        "I'd recommend castling soon to protect your king."
    ];

    private readonly Random _random = new();
    private CancellationTokenSource? _responseCts;
    private bool _disposed;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => State == ConnectionState.Connected;
    public bool SupportsVideo => false; // Mock does not process video
    public string ProviderName => "Mock Provider";

    public async Task ConnectAsync(Agent agent)
    {
        if (State != ConnectionState.Disconnected) return;

        State = ConnectionState.Connecting;
        ConnectionStateChanged?.Invoke(this, State);

        // Simulate connection delay
        await Task.Delay(1500);

        State = ConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, State);

        StartMockResponses();
    }

    public Task DisconnectAsync()
    {
        State = ConnectionState.Disconnecting;
        ConnectionStateChanged?.Invoke(this, State);

        StopMockResponses();

        State = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, State);

        return Task.CompletedTask;
    }

    public Task SendAudioAsync(byte[] audioData)
    {
        // Mock provider ignores audio input
        return Task.CompletedTask;
    }

    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg")
    {
        // Mock provider ignores image input
        return Task.CompletedTask;
    }

    public Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        // Mock provider accepts text but does not forward; simulates success
        return Task.CompletedTask;
    }

    public Task SendContextualUpdateAsync(string contextText, CancellationToken ct = default)
    {
        // Mock provider ignores contextual updates
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopMockResponses();
    }

    private void StartMockResponses()
    {
        _responseCts = new CancellationTokenSource();
        var token = _responseCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && (State == ConnectionState.Connected || State == ConnectionState.Reconnecting))
            {
                try
                {
                    await Task.Delay(_random.Next(5000, 15000), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested || (State != ConnectionState.Connected && State != ConnectionState.Reconnecting))
                    break;

                var response = MockResponses[_random.Next(MockResponses.Length)];
                TextReceived?.Invoke(this, response);
            }
        }, token);
    }

    private void StopMockResponses()
    {
        try
        {
            _responseCts?.Cancel();
            _responseCts?.Dispose();
        }
        catch
        {
            // Best effort
        }
        finally
        {
            _responseCts = null;
        }
    }
}

