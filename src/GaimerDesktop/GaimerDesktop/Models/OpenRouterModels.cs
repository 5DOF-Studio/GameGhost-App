using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaimerDesktop.Models;

// ── Request Models ──────────────────────────────────────────────────────────

/// <summary>
/// OpenRouter chat completion request. Compatible with OpenAI API format.
/// </summary>
public class OpenRouterRequest
{
    public string Model { get; set; } = string.Empty;
    public List<OpenRouterMessage> Messages { get; set; } = new();
    public List<OpenRouterTool>? Tools { get; set; }
    public string? ToolChoice { get; set; }
    public bool Stream { get; set; }
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

/// <summary>
/// A single message in the chat completion conversation.
/// Content can be a plain string or a list of ContentPart for vision (multimodal).
/// </summary>
public class OpenRouterMessage
{
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// String for text-only messages, or List&lt;ContentPart&gt; for vision/multimodal.
    /// </summary>
    [JsonConverter(typeof(ContentConverter))]
    public object? Content { get; set; }

    public List<ToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
}

/// <summary>
/// Part of a multimodal content array (text or image_url).
/// </summary>
public class ContentPart
{
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public ImageUrl? ImageUrl { get; set; }
}

/// <summary>
/// Image URL payload for vision requests. Supports data:image/jpeg;base64,... format.
/// </summary>
public class ImageUrl
{
    public string Url { get; set; } = string.Empty;
}

// ── Tool Models ─────────────────────────────────────────────────────────────

/// <summary>
/// Tool definition in the OpenAI function calling format.
/// </summary>
public class OpenRouterTool
{
    public string Type { get; set; } = "function";
    public OpenRouterFunction Function { get; set; } = new();
}

/// <summary>
/// Function definition within a tool.
/// </summary>
public class OpenRouterFunction
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement Parameters { get; set; }
}

/// <summary>
/// A tool call returned by the model.
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public ToolCallFunction Function { get; set; } = new();
}

/// <summary>
/// Function invocation details within a tool call.
/// Arguments is a JSON string that must be parsed by the tool executor.
/// </summary>
public class ToolCallFunction
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

// ── Response Models ─────────────────────────────────────────────────────────

/// <summary>
/// OpenRouter chat completion response (non-streaming).
/// </summary>
public class OpenRouterResponse
{
    public string Id { get; set; } = string.Empty;
    public List<Choice> Choices { get; set; } = new();
    public Usage? Usage { get; set; }
}

/// <summary>
/// A single choice in the completion response.
/// </summary>
public class Choice
{
    public int Index { get; set; }
    public OpenRouterMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

/// <summary>
/// Token usage statistics.
/// </summary>
public class Usage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

// ── Streaming Models ────────────────────────────────────────────────────────

/// <summary>
/// A single SSE chunk in a streaming chat completion response.
/// </summary>
public class OpenRouterStreamChunk
{
    public string Id { get; set; } = string.Empty;
    public List<StreamChoice> Choices { get; set; } = new();
}

/// <summary>
/// A single choice delta in a streaming chunk.
/// </summary>
public class StreamChoice
{
    public int Index { get; set; }
    public OpenRouterMessage Delta { get; set; } = new();
    public string? FinishReason { get; set; }
}

// ── Content Converter ───────────────────────────────────────────────────────

/// <summary>
/// Handles polymorphic Content field: deserializes as string or List&lt;ContentPart&gt;.
/// </summary>
public class ContentConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return JsonSerializer.Deserialize<List<ContentPart>>(ref reader, options);
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // Skip unknown tokens
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case List<ContentPart> parts:
                JsonSerializer.Serialize(writer, parts, options);
                break;
            default:
                JsonSerializer.Serialize(writer, value, options);
                break;
        }
    }
}

// ── STJ Source Generator Context ────────────────────────────────────────────

[JsonSerializable(typeof(OpenRouterRequest))]
[JsonSerializable(typeof(OpenRouterResponse))]
[JsonSerializable(typeof(OpenRouterStreamChunk))]
[JsonSerializable(typeof(List<OpenRouterMessage>))]
[JsonSerializable(typeof(List<ContentPart>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class OpenRouterJsonContext : JsonSerializerContext { }
