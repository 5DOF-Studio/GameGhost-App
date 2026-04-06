using System.Text.Json;
using System.Text.Json.Serialization;

namespace WitnessDesktop.Models;

/// <summary>
/// Structured brain analysis output. Matches the JSON schema sent via response_format.
/// The visual_description field captures chain-of-thought grounding from the prompt.
/// All fields nullable for graceful degradation when model partially complies.
/// </summary>
public class BrainAnalysisResult
{
    [JsonPropertyName("visual_description")]
    public string? VisualDescription { get; set; }

    [JsonPropertyName("last_move")]
    public string? LastMove { get; set; }

    [JsonPropertyName("position_assessment")]
    public string? PositionAssessment { get; set; }

    [JsonPropertyName("threats")]
    public string? Threats { get; set; }

    [JsonPropertyName("suggested_action")]
    public string? SuggestedAction { get; set; }

    [JsonPropertyName("fen")]
    public string? Fen { get; set; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }

    /// <summary>Raw parsed observations keyed by field name from pack schema.</summary>
    [JsonIgnore]
    public Dictionary<string, JsonElement>? Observations { get; set; }

    public string ToDisplayText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(PositionAssessment))
            parts.Add(PositionAssessment);
        if (!string.IsNullOrWhiteSpace(Threats))
            parts.Add($"Threats: {Threats}");
        if (!string.IsNullOrWhiteSpace(SuggestedAction))
            parts.Add($"Suggestion: {SuggestedAction}");
        if (!string.IsNullOrWhiteSpace(Fen))
            parts.Add($"[FEN: {Fen}]");
        return parts.Count > 0 ? string.Join("\n", parts) : "Analysis complete (no details).";
    }

    public double ConfidenceScore => Confidence?.ToUpperInvariant() switch
    {
        "CERTAIN" => 0.95,
        "LIKELY" => 0.85,
        "UNCERTAIN" => 0.6,
        "GUESSING" => 0.3,
        _ => 0.5
    };
}
