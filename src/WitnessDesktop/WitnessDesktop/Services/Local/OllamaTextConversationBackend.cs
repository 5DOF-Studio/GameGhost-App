using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WitnessDesktop.Services.Local;

/// <summary>
/// Ollama-backed conversational intelligence backend for local voice.
/// Uses /api/chat with streaming disabled — same pattern as OllamaLocalVisionInferenceClient.
/// This is the transitional first backend; the app depends only on ILocalTextConversationBackend.
/// </summary>
public sealed class OllamaTextConversationBackend : ILocalTextConversationBackend
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;

    public OllamaTextConversationBackend(HttpClient httpClient, string modelId)
    {
        _httpClient = httpClient;
        _modelId = string.IsNullOrWhiteSpace(modelId) ? "minicpm-v" : modelId;
    }

    public string RuntimeName => "ollama";

    public async Task<string> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default)
    {
        var payload = new OllamaChatRequest
        {
            Model = _modelId,
            Stream = false,
            Messages = history.Select(m => new OllamaChatMsg { Role = m.Role, Content = m.Content }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, cancellationToken: ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(chat?.Message?.Content))
        {
            throw new InvalidOperationException("Ollama returned an empty chat response.");
        }

        return chat.Message.Content;
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<OllamaChatMsg> Messages { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }
    }

    private sealed class OllamaChatMsg
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMsgResponse? Message { get; init; }
    }

    private sealed class OllamaChatMsgResponse
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
