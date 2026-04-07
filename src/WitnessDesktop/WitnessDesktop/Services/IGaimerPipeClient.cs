namespace WitnessDesktop.Services;

public interface IGaimerPipeClient : IDisposable
{
    bool IsConnected { get; }
    event EventHandler<string>? MessageReceived;
    event EventHandler? ConnectionLost;

    Task<bool> ConnectAsync(string socketPath, CancellationToken ct = default);
    Task SendAsync(string jsonLine, CancellationToken ct = default);
    void Disconnect();
}
