using System.Text.Json;
using System.Text.Json.Serialization;

namespace WitnessDesktop.Models;

public class GameSkillPack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("genre")]
    public string Genre { get; set; } = "";

    [JsonPropertyName("observationSchema")]
    public ObservationSchema ObservationSchema { get; set; } = new();

    [JsonPropertyName("eventMapping")]
    public List<EventMappingEntry> EventMapping { get; set; } = new();

    [JsonPropertyName("topStripField")]
    public string TopStripField { get; set; } = "";

    [JsonPropertyName("userPromptTemplate")]
    public string? UserPromptTemplate { get; set; }

    [JsonPropertyName("brainInstructions")]
    public string BrainInstructions { get; set; } = "brain-instructions.md";

    /// <summary>Loaded markdown content of brain-instructions.md. Not serialized.</summary>
    [JsonIgnore]
    public string BrainInstructionsContent { get; set; } = "";

    /// <summary>
    /// Silence timer preset for exchange timeout. Quick=8s (FPS), Normal=15s (chess), Patient=30s (RPG).
    /// Read by ExchangeManager on exchange open. Default: Normal.
    /// </summary>
    [JsonPropertyName("silenceTimeoutPreset")]
    public string SilenceTimeoutPreset { get; set; } = "Normal";

    /// <summary>
    /// Pack-specific grounding language for voice corrections when context is stale or missing.
    /// Replaces hardcoded chess-specific "checking the board" language in MainViewModel.
    /// </summary>
    [JsonPropertyName("groundingLanguage")]
    public GroundingLanguage GroundingLanguage { get; set; } = new();

    /// <summary>
    /// Pack-specific regex patterns for voice turn classification.
    /// Replaces hardcoded chess patterns in VoiceGroundingCoordinator.
    /// </summary>
    [JsonPropertyName("voiceClassification")]
    public VoiceClassificationPatterns VoiceClassification { get; set; } = new();

    /// <summary>Absolute path to the pack directory on disk. Not serialized.</summary>
    [JsonIgnore]
    public string PackDirectory { get; set; } = "";
}

public class ObservationSchema
{
    [JsonPropertyName("schemaName")]
    public string SchemaName { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<ObservationField> Fields { get; set; } = new();

    /// <summary>
    /// Generates the OpenRouter response_format JSON schema from field definitions.
    /// </summary>
    public JsonElement BuildResponseFormat()
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var field in Fields)
        {
            var prop = new Dictionary<string, object> { ["description"] = field.Description };

            switch (field.Type)
            {
                case "string":
                    prop["type"] = "string";
                    break;
                case "string?":
                    prop["type"] = new[] { "string", "null" };
                    break;
                case "enum":
                    prop["type"] = "string";
                    if (field.Values is { Count: > 0 })
                        prop["enum"] = field.Values;
                    break;
                case "int":
                    prop["type"] = new[] { "integer", "null" };
                    break;
                case "object":
                case "array":
                    prop["type"] = new[] { "string", "null" };
                    break;
                default:
                    prop["type"] = new[] { "string", "null" };
                    break;
            }

            properties[field.Key] = prop;
            if (field.Required)
                required.Add(field.Key);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "json_schema",
            ["json_schema"] = new Dictionary<string, object>
            {
                ["name"] = SchemaName,
                ["strict"] = true,
                ["schema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                    ["additionalProperties"] = false
                }
            }
        };

        var json = JsonSerializer.Serialize(schema);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

public class ObservationField
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("route")]
    public string Route { get; set; } = "temporal";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("values")]
    public List<string>? Values { get; set; }
}

public class EventMappingEntry
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = "";
}

/// <summary>
/// Pack-specific regex patterns for classifying voice turns as game-state-sensitive
/// or general game knowledge. Replaces hardcoded chess patterns.
/// </summary>
public class VoiceClassificationPatterns
{
    /// <summary>Regex patterns for questions about CURRENT game state (replaces BoardSensitiveRegex).
    /// Chess: "what piece", "am I winning", "fork", "pin"
    /// FPS: "where's the bomb", "enemy position", "are we winning the round"</summary>
    [JsonPropertyName("gameStateSensitive")]
    public List<string> GameStateSensitive { get; set; } = new();

    /// <summary>Regex patterns for general game knowledge not requiring live state (replaces GeneralGameRegex).
    /// Chess: "what's a Sicilian", "explain the London"
    /// FPS: "what's hardpoint", "explain cyber attack rules"</summary>
    [JsonPropertyName("generalKnowledge")]
    public List<string> GeneralKnowledge { get; set; } = new();
}

/// <summary>
/// Pack-specific language for voice grounding corrections.
/// Each pack defines how the agent communicates about stale or missing game state.
/// </summary>
public class GroundingLanguage
{
    /// <summary>Display text when refreshing context (e.g., "checking the board…").</summary>
    [JsonPropertyName("refreshDisplay")]
    public string RefreshDisplay { get; set; } = "checking on that…";

    /// <summary>Display text when context is stale (e.g., "board info may be outdated").</summary>
    [JsonPropertyName("staleDisplay")]
    public string StaleDisplay { get; set; } = "info may be outdated";

    /// <summary>Display text when no context available (e.g., "no current board info").</summary>
    [JsonPropertyName("unavailableDisplay")]
    public string UnavailableDisplay { get; set; } = "no current info";

    /// <summary>System correction injected to voice when refreshing.</summary>
    [JsonPropertyName("refreshCorrection")]
    public string RefreshCorrection { get; set; } =
        "[SYSTEM: You do not have current game information. Do NOT make specific claims about the current game state. Say you are checking and will update shortly.]";

    /// <summary>System correction injected to voice when context is stale.</summary>
    [JsonPropertyName("staleCorrection")]
    public string StaleCorrection { get; set; } =
        "[SYSTEM: Your game information may be outdated. Express uncertainty about specifics. Use phrases like 'from what I last saw' or 'I'm not fully certain'.]";

    /// <summary>System correction injected to voice when no context.</summary>
    [JsonPropertyName("unavailableCorrection")]
    public string UnavailableCorrection { get; set; } =
        "[SYSTEM: No game information is available. Do NOT make claims about the current game state. If asked, honestly say you cannot see the game right now.]";
}
