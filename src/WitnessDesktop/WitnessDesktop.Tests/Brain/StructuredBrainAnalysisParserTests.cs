using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Brain;

public class StructuredBrainAnalysisParserTests
{
    // ── TryParseLabeledText: full markdown-labeled input ─────────────────────

    [Fact]
    public void TryParseLabeledText_FullMarkdownLabeled_ParsesAllSections()
    {
        var text = """
            **VISUAL DESCRIPTION:** White pieces control the center with pawns on e4 and d4.
            **POSITION ASSESSMENT:** White is slightly better due to space advantage.
            **THREATS:** Knight fork on f7 is possible.
            **SUGGESTED ACTION:** Develop Nf3 to reinforce center.
            """;

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.VisualDescription.Should().Contain("White pieces control the center");
        result.PositionAssessment.Should().Contain("White is slightly better");
        result.Threats.Should().Contain("Knight fork on f7");
        result.SuggestedAction.Should().Contain("Develop Nf3");
        // No raw ** markers in recovered content
        result.VisualDescription.Should().NotContain("**");
        result.PositionAssessment.Should().NotContain("**");
    }

    [Fact]
    public void TryParseLabeledText_WithOptionalFields_ParsesFenAndConfidence()
    {
        var text = """
            **VISUAL DESCRIPTION:** A chess board mid-game.
            **POSITION ASSESSMENT:** Equal position.
            **FEN:** rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1
            **CONFIDENCE:** LIKELY
            **LAST MOVE:** e2e4
            """;

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.Fen.Should().Contain("rnbqkbnr");
        result.Confidence.Should().Be("LIKELY");
        result.LastMove.Should().Be("e2e4");
    }

    // ── Label variant tolerance ──────────────────────────────────────────────

    [Fact]
    public void TryParseLabeledText_MixedCase_StillParses()
    {
        var text = """
            Visual Description: Board shows opening position.
            Position Assessment: Even game.
            """;

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.VisualDescription.Should().Contain("Board shows opening position");
        result.PositionAssessment.Should().Contain("Even game");
    }

    [Fact]
    public void TryParseLabeledText_WithoutMarkdownEmphasis_StillParses()
    {
        var text = """
            VISUAL DESCRIPTION: Standard Sicilian Defense setup.
            POSITION ASSESSMENT: Slightly better for White.
            THREATS: None immediate.
            SUGGESTED ACTION: Castle kingside.
            """;

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.VisualDescription.Should().Contain("Standard Sicilian");
        result.SuggestedAction.Should().Contain("Castle kingside");
    }

    [Fact]
    public void TryParseLabeledText_ExtraWhitespaceAroundColons_StillParses()
    {
        var text = """
            **POSITION ASSESSMENT** : White has advantage.
            **THREATS** : Back rank mate threat.
            """;

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.PositionAssessment.Should().Contain("White has advantage");
        result.Threats.Should().Contain("Back rank mate");
    }

    // ── Partial recovery ─────────────────────────────────────────────────────

    [Fact]
    public void TryParseLabeledText_PositionAssessmentAlone_Succeeds()
    {
        var text = "**POSITION ASSESSMENT:** The game is roughly equal with chances for both sides.";

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.PositionAssessment.Should().Contain("roughly equal");
    }

    [Fact]
    public void TryParseLabeledText_TwoMainSections_Succeeds()
    {
        var text = """
            **VISUAL DESCRIPTION:** Chess board with pieces.
            **THREATS:** Knight attacking queen.
            """;

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().NotBeNull();
        result!.VisualDescription.Should().Contain("Chess board");
        result.Threats.Should().Contain("Knight attacking queen");
    }

    [Fact]
    public void TryParseLabeledText_OnlyOneSectionNotAssessment_ReturnsNull()
    {
        var text = "**VISUAL DESCRIPTION:** Some board description only.";

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().BeNull();
    }

    // ── Non-matching input ───────────────────────────────────────────────────

    [Fact]
    public void TryParseLabeledText_PlainText_ReturnsNull()
    {
        var text = "The position shows a strong center for White with pawns on e4 and d4.";

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(text);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseLabeledText_EmptyString_ReturnsNull()
    {
        StructuredBrainAnalysisParser.TryParseLabeledText("").Should().BeNull();
        StructuredBrainAnalysisParser.TryParseLabeledText("  ").Should().BeNull();
        StructuredBrainAnalysisParser.TryParseLabeledText(null!).Should().BeNull();
    }

    [Fact]
    public void TryParseLabeledText_ValidJson_ReturnsNull()
    {
        var json = """{"visual_description":"Board","position_assessment":"Even"}""";

        var result = StructuredBrainAnalysisParser.TryParseLabeledText(json);

        // JSON doesn't have labeled sections on separate lines — should return null
        // (JSON path is handled by TryParseStructuredAnalysis, not this method)
        result.Should().BeNull();
    }

    // ── SanitizeFallbackText ─────────────────────────────────────────────────

    [Fact]
    public void SanitizeFallbackText_RemovesDoubleAsterisks()
    {
        var text = "**VISUAL DESCRIPTION:** unclear board state\n**NOTES:** model uncertain";

        var result = StructuredBrainAnalysisParser.SanitizeFallbackText(text);

        result.Should().NotContain("**");
    }

    [Fact]
    public void SanitizeFallbackText_RemovesSectionLabels()
    {
        var text = "**VISUAL DESCRIPTION:** White controls center.\n**POSITION ASSESSMENT:** Slightly better for White.";

        var result = StructuredBrainAnalysisParser.SanitizeFallbackText(text);

        result.Should().NotContain("VISUAL DESCRIPTION:");
        result.Should().NotContain("POSITION ASSESSMENT:");
        result.Should().Contain("White controls center");
        result.Should().Contain("Slightly better for White");
    }

    [Fact]
    public void SanitizeFallbackText_PreservesSemanticContent()
    {
        var text = "**POSITION ASSESSMENT:** The knight on f3 is well-placed.\n**THREATS:** Back rank mate possible.";

        var result = StructuredBrainAnalysisParser.SanitizeFallbackText(text);

        result.Should().Contain("knight on f3 is well-placed");
        result.Should().Contain("Back rank mate possible");
    }

    [Fact]
    public void SanitizeFallbackText_PlainText_PassesThrough()
    {
        var text = "The position is roughly equal with chances for both sides.";

        var result = StructuredBrainAnalysisParser.SanitizeFallbackText(text);

        result.Should().Be(text);
    }

    [Fact]
    public void SanitizeFallbackText_EmptyInput_PassesThrough()
    {
        StructuredBrainAnalysisParser.SanitizeFallbackText("").Should().Be("");
        StructuredBrainAnalysisParser.SanitizeFallbackText("  ").Should().Be("  ");
    }

    [Fact]
    public void SanitizeFallbackText_StripsMarkdownEmphasis_KeepsInnerText()
    {
        var text = "The **queen** is under *attack* from the knight.";

        var result = StructuredBrainAnalysisParser.SanitizeFallbackText(text);

        result.Should().Contain("queen");
        result.Should().Contain("attack");
        result.Should().NotContain("**");
        result.Should().NotContain("*attack*");
    }

    [Fact]
    public void SanitizeFallbackText_CleansExcessiveNewlines()
    {
        var text = "Line one.\n\n\n\n\nLine two.";

        var result = StructuredBrainAnalysisParser.SanitizeFallbackText(text);

        result.Should().Be("Line one.\n\nLine two.");
    }
}
