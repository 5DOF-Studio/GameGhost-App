namespace WitnessDesktop.Services.Local;

/// <summary>
/// Runtime-neutral abstraction for local conversational intelligence.
/// Accepts conversation history, returns response text. No audio, no events, no connection lifecycle.
/// Implementations: OllamaTextConversationBackend (transitional), future direct process or alternative server.
/// </summary>
public interface ILocalTextConversationBackend
{
    Task<string> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default);
    string RuntimeName { get; }
}

/// <summary>
/// Simple DTO for conversation history entries passed to local backends.
/// </summary>
public sealed record ConversationMessage(string Role, string Content);
