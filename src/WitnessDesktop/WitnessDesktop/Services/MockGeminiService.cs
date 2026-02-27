using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

public class MockGeminiService : IGeminiService
{
    private readonly string[] _mockResponses =
    [
        "Nice move! I see you're developing your pieces well.",
        "That's a solid strategy. Keep the pressure on!",
        "Interesting position. Have you considered the knight fork?",
        "GG! You're playing really well today.",
        "I'd recommend castling soon to protect your king."
    ];

    private readonly Random _random = new();

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<string>? TextReceived;
    public event EventHandler? Interrupted;
    public event EventHandler<string>? ErrorOccurred;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => State == ConnectionState.Connected;

    public async Task ConnectAsync(Agent agent)
    {
        if (State != ConnectionState.Disconnected) return;

        State = ConnectionState.Connecting;
        ConnectionStateChanged?.Invoke(this, State);

        await Task.Delay(1500);

        State = ConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, State);

        StartMockResponses();
    }

    public async Task DisconnectAsync()
    {
        State = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, State);
        await Task.CompletedTask;
    }

    public Task SendAudioAsync(byte[] audioData)
    {
        return Task.CompletedTask;
    }

    public Task SendImageAsync(byte[] imageData, string mimeType = "image/jpeg")
    {
        return Task.CompletedTask;
    }

    private void StartMockResponses()
    {
        Task.Run(async () =>
        {
            while (State == ConnectionState.Connected || State == ConnectionState.Reconnecting)
            {
                await Task.Delay(_random.Next(5000, 15000));
                if (State != ConnectionState.Connected && State != ConnectionState.Reconnecting) break;

                var response = _mockResponses[_random.Next(_mockResponses.Length)];
                TextReceived?.Invoke(this, response);
            }
        });
    }

    public void Dispose()
    {
        // Mock service - nothing to dispose
    }
}

