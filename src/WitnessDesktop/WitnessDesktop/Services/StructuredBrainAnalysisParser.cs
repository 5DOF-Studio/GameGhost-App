using System.Text.RegularExpressions;
using WitnessDesktop.Models;

namespace WitnessDesktop.Services;

/// <summary>
/// Parses and sanitizes brain analysis text that may arrive as markdown-labeled
/// pseudo-structured output instead of strict JSON. Provides heuristic recovery
/// and display sanitization to prevent raw formatting artifacts from reaching users.
/// </summary>
internal static class StructuredBrainAnalysisParser
{
    // Matches labeled sections like **VISUAL DESCRIPTION:** or POSITION ASSESSMENT:
    // Tolerates markdown emphasis, mixed case, optional whitespace around colon
    private static readonly Regex LabeledSectionRegex = new(
        @"(?:^|\n)\s*(?:\*{1,2})?\s*(?<label>VISUAL\s+DESCRIPTION|POSITION\s+ASSESSMENT|THREATS|SUGGESTED\s+ACTION|LAST\s+MOVE|FEN|CONFIDENCE)\s*(?:\*{1,2})?\s*:\s*(?<content>(?:(?!(?:^|\n)\s*(?:\*{1,2})?\s*(?:VISUAL\s+DESCRIPTION|POSITION\s+ASSESSMENT|THREATS|SUGGESTED\s+ACTION|LAST\s+MOVE|FEN|CONFIDENCE)\s*(?:\*{1,2})?\s*:).)*)".Trim(),
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // Matches markdown emphasis markers: **text**, *text*, ## text
    private static readonly Regex MarkdownEmphasisRegex = new(
        @"\*{1,2}([^*]+)\*{1,2}",
        RegexOptions.Compiled);

    // Matches section label patterns that should not appear in user-facing text
    private static readonly Regex SectionLabelRegex = new(
        @"(?:^|\n)\s*(?:\*{1,2})?\s*(?:VISUAL\s+DESCRIPTION|POSITION\s+ASSESSMENT|THREATS|SUGGESTED\s+ACTION|LAST\s+MOVE|FEN|CONFIDENCE)\s*(?:\*{1,2})?\s*:\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Attempts to parse markdown-labeled pseudo-structured brain output into a BrainAnalysisResult.
    /// Returns null if the text doesn't contain enough recoverable labeled sections.
    /// Minimum: position_assessment alone, or any two of the four main sections.
    /// </summary>
    internal static BrainAnalysisResult? TryParseLabeledText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var matches = LabeledSectionRegex.Matches(text);
        if (matches.Count == 0) return null;

        string? visualDescription = null;
        string? positionAssessment = null;
        string? threats = null;
        string? suggestedAction = null;
        string? lastMove = null;
        string? fen = null;
        string? confidence = null;

        foreach (Match match in matches)
        {
            var label = NormalizeLabel(match.Groups["label"].Value);
            var content = CleanContent(match.Groups["content"].Value);
            if (string.IsNullOrWhiteSpace(content)) continue;

            switch (label)
            {
                case "VISUAL DESCRIPTION": visualDescription = content; break;
                case "POSITION ASSESSMENT": positionAssessment = content; break;
                case "THREATS": threats = content; break;
                case "SUGGESTED ACTION": suggestedAction = content; break;
                case "LAST MOVE": lastMove = content; break;
                case "FEN": fen = content; break;
                case "CONFIDENCE": confidence = content; break;
            }
        }

        // Minimum useful recovery: position_assessment alone, or any 2 main sections
        var mainSectionCount = new[] { visualDescription, positionAssessment, threats, suggestedAction }
            .Count(s => !string.IsNullOrWhiteSpace(s));

        if (positionAssessment != null || mainSectionCount >= 2)
        {
            return new BrainAnalysisResult
            {
                VisualDescription = visualDescription,
                PositionAssessment = positionAssessment,
                Threats = threats,
                SuggestedAction = suggestedAction,
                LastMove = lastMove,
                Fen = fen,
                Confidence = confidence,
            };
        }

        return null;
    }

    /// <summary>
    /// Sanitizes brain analysis text for display by removing markdown formatting artifacts
    /// and section labels that should not appear in user-facing surfaces.
    /// </summary>
    internal static string SanitizeFallbackText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Strip section label patterns (e.g., **VISUAL DESCRIPTION:**)
        var result = SectionLabelRegex.Replace(text, "\n");

        // Strip remaining markdown emphasis markers, keeping inner text
        result = MarkdownEmphasisRegex.Replace(result, "$1");

        // Strip any remaining standalone asterisks
        result = result.Replace("**", "");

        // Clean up excessive whitespace
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        result = result.Trim();

        return result;
    }

    private static string NormalizeLabel(string label)
    {
        return Regex.Replace(label.Trim().ToUpperInvariant(), @"\s+", " ");
    }

    private static string CleanContent(string content)
    {
        // Remove markdown emphasis from content
        var cleaned = MarkdownEmphasisRegex.Replace(content, "$1");
        cleaned = cleaned.Replace("**", "");
        return cleaned.Trim();
    }
}
