using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WitnessDesktop.Services.Local;

/// <summary>
/// Ollama-backed local inference client for MiniCPM vision/chat traffic.
/// Uses /api/chat with streaming disabled so the existing brain channel contract stays simple.
/// </summary>
public sealed class OllamaLocalVisionInferenceClient : ILocalVisionInferenceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _defaultModelId;

    public OllamaLocalVisionInferenceClient(HttpClient httpClient, string defaultModelId)
    {
        _httpClient = httpClient;
        _defaultModelId = string.IsNullOrWhiteSpace(defaultModelId) ? "minicpm-v" : defaultModelId;
    }

    public async Task<LocalVisionResponse> AnalyzeImageAsync(LocalVisionRequest request, CancellationToken ct = default)
    {
        var modelId = ResolveModelId(request.ModelId);
        var payload = new OllamaChatRequest
        {
            Model = modelId,
            Stream = false,
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = request.SystemPrompt
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = request.UserPrompt,
                    Images = [Convert.ToBase64String(request.ImageData)]
                }
            ]
        };

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, cancellationToken: ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct).ConfigureAwait(false);
        if (chat?.Message?.Content is null)
        {
            throw new InvalidOperationException("Ollama returned an empty chat response.");
        }

        sw.Stop();

        return new LocalVisionResponse
        {
            AssistantText = chat.Message.Content,
            Success = true,
            ModelId = chat.Model ?? modelId,
            LatencyMs = chat.TotalDuration is > 0
                ? (int)Math.Max(1, TimeSpan.FromTicks(chat.TotalDuration.Value / 100).TotalMilliseconds)
                : (int)Math.Max(1, sw.Elapsed.TotalMilliseconds)
        };
    }

    public async Task<string> ChatAsync(string userQuery, string systemPrompt, CancellationToken ct = default)
    {
        var payload = new OllamaChatRequest
        {
            Model = _defaultModelId,
            Stream = false,
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = systemPrompt
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = userQuery
                }
            ]
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, cancellationToken: ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct).ConfigureAwait(false);
        return chat?.Message?.Content ?? throw new InvalidOperationException("Ollama returned an empty chat response.");
    }

    private string ResolveModelId(string? overrideModelId)
    {
        return string.IsNullOrWhiteSpace(overrideModelId) ? _defaultModelId : overrideModelId;
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<OllamaChatMessage> Messages { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }

        [JsonPropertyName("images")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("message")]
        public OllamaChatMessageResponse? Message { get; init; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; init; }
    }

    private sealed class OllamaChatMessageResponse
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
