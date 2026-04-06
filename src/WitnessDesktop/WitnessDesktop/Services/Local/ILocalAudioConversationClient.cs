using WitnessDesktop.Models;

namespace WitnessDesktop.Services.Local;

/// <summary>
/// Minimal local audio conversation client abstraction for staged local voice support.
/// Provider adapters wrap this to preserve the existing IConversationProvider contract.
/// </summary>
public interface ILocalAudioConversationClient : IDisposable
{
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    event EventHandler<byte[]>? AudioReceived;
    event EventHandler<string>? TextReceived;
    event EventHandler? Interrupted;
    event EventHandler<string>? ErrorOccurred;

    bool IsConnected { get; }
    string RuntimeName { get; }

    Task ConnectAsync(Agent agent, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendAudioAsync(byte[] audioData, CancellationToken cancellationToken = default);
    Task SendTextAsync(string text, CancellationToken cancellationToken = default);
    Task SendContextualUpdateAsync(string contextText, CancellationToken cancellationToken = default);
}
