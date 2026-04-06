using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Services.Brain;

/// <summary>
/// HTTP client wrapper for the OpenRouter REST API (OpenAI-compatible format).
/// Handles chat completions (sync + streaming), vision requests, and tool calling.
/// Stateless per-request; thread safety comes from HttpClient being thread-safe.
/// </summary>
public sealed class OpenRouterClient
{
    private readonly HttpClient _httpClient;
    private readonly string _defaultModel;
    private readonly IGameSkillPackService? _packService;

    /// <summary>
    /// Creates a new OpenRouter API client.
    /// </summary>
    /// <param name="httpClient">Injected HttpClient (lifecycle managed by caller/DI).</param>
    /// <param name="apiKey">OpenRouter API key (sk-or-...).</param>
    /// <param name="defaultModel">Default model identifier (e.g., "google/gemini-2.0-flash-001").</param>
    /// <param name="packService">Optional skill pack service for dynamic schema generation.</param>
    public OpenRouterClient(HttpClient httpClient, string apiKey, string defaultModel,
        IGameSkillPackService? packService = null)
    {
        _httpClient = httpClient;
        _defaultModel = defaultModel;
        _packService = packService;

        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Add("X-Title", "Game Ghost");
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ── Sync Completion ──────────────────────────────────────────────────────

    /// <summary>
    /// Send a chat completion request and return the full response.
    /// </summary>
    public async Task<OpenRouterResponse> ChatCompletionAsync(
        OpenRouterRequest request,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, OpenRouterJsonContext.Default.OpenRouterRequest);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("chat/completions", content, ct)
            .ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new OpenRouterException(response.StatusCode, responseBody);
        }

        var result = JsonSerializer.Deserialize(responseBody, OpenRouterJsonContext.Default.OpenRouterResponse);
        return result ?? throw new OpenRouterException(
            response.StatusCode,
            "Deserialization returned null for response body: " + responseBody);
    }

    // ── Streaming Completion (SSE) ───────────────────────────────────────────

    /// <summary>
    /// Send a streaming chat completion request. Yields chunks as they arrive via SSE.
    /// Forces Stream=true on the request.
    /// </summary>
    public async IAsyncEnumerable<OpenRouterStreamChunk> ChatCompletionStreamAsync(
        OpenRouterRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        request.Stream = true;

        var json = JsonSerializer.Serialize(request, OpenRouterJsonContext.Default.OpenRouterRequest);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new OpenRouterException(response.StatusCode, errorBody);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // SSE format: "data: {json}" or "data: [DONE]"
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var data = line.Substring(6); // Skip "data: " prefix

            if (data == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize(
                data,
                OpenRouterJsonContext.Default.OpenRouterStreamChunk);

            if (chunk is not null)
                yield return chunk;
        }
    }

    // ── Request Builder Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Build a vision analysis request with a base64-encoded image and text context prompt.
    /// </summary>
    /// <param name="imageData">PNG image bytes.</param>
    /// <param name="contextPrompt">Text instructions for the vision model.</param>
    /// <param name="model">Model override (defaults to constructor default).</param>
    /// <param name="systemPrompt">Optional system message prepended before the user message.</param>
    public OpenRouterRequest CreateImageAnalysisRequest(
        byte[] imageData,
        string contextPrompt,
        string? model = null,
        string? systemPrompt = null)
    {
        var base64 = Convert.ToBase64String(imageData);

        // Detect MIME type from magic bytes (JPEG starts with FF D8, PNG with 89 50)
        var mime = imageData.Length >= 2 && imageData[0] == 0xFF && imageData[1] == 0xD8
            ? "image/jpeg"
            : "image/png";

        var messages = new List<OpenRouterMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OpenRouterMessage { Role = "system", Content = systemPrompt });
        }

        messages.Add(new OpenRouterMessage
        {
            Role = "user",
            Content = new List<ContentPart>
            {
                new() { Type = "text", Text = contextPrompt },
                new()
                {
                    Type = "image_url",
                    ImageUrl = new ImageUrl { Url = $"data:{mime};base64,{base64}" }
                }
            }
        });

        var request = new OpenRouterRequest
        {
            Model = model ?? _defaultModel,
            Messages = messages,
            MaxTokens = 1024
        };

        // If model is Gemini, request Google-only routing (no fallback to other providers)
        if ((model ?? _defaultModel).StartsWith("google/gemini", StringComparison.Ordinal))
        {
            var providerJson = """{"order":["Google"],"allow_fallbacks":false}""";
            using var providerDoc = JsonDocument.Parse(providerJson);
            request.Provider = providerDoc.RootElement.Clone();
        }

        // Dynamic schema from active pack, or generic fallback
        var pack = _packService?.ActivePack;
        if (pack != null)
        {
            request.ResponseFormat = pack.ObservationSchema.BuildResponseFormat();
        }
        else
        {
            // Generic fallback schema
            var fallbackSchema = new ObservationSchema
            {
                SchemaName = "generic_analysis",
                Fields = new List<ObservationField>
                {
                    new() { Key = "visual_description", Type = "string", Required = true, Description = "What you see" },
                    new() { Key = "assessment", Type = "string", Required = true, Description = "Current state assessment" },
                    new() { Key = "confidence", Type = "enum", Required = true, Description = "Confidence level",
                             Values = new() { "CERTAIN", "LIKELY", "UNCERTAIN", "GUESSING" } },
                }
            };
            request.ResponseFormat = fallbackSchema.BuildResponseFormat();
        }

        return request;
    }

    /// <summary>
    /// Build a tool-calling request with messages and tool definitions.
    /// </summary>
    /// <param name="messages">Conversation history (may include prior tool results).</param>
    /// <param name="tools">Available tool definitions.</param>
    /// <param name="model">Model override (defaults to constructor default).</param>
    public OpenRouterRequest CreateToolCallRequest(
        List<OpenRouterMessage> messages,
        List<OpenRouterTool> tools,
        string? model = null)
    {
        return new OpenRouterRequest
        {
            Model = model ?? _defaultModel,
            Messages = messages,
            Tools = tools,
            ToolChoice = "auto"
        };
    }
}

// ── Exception ────────────────────────────────────────────────────────────────

/// <summary>
/// Exception thrown when an OpenRouter API call returns a non-success HTTP status code.
/// Includes the status code and raw response body for diagnostics.
/// </summary>
public class OpenRouterException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }

    public OpenRouterException(HttpStatusCode statusCode, string responseBody)
        : base($"OpenRouter API error ({(int)statusCode} {statusCode}): {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
