using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Brain;

public class BrainAnalysisResultTests
{
    // ── ToDisplayText ────────────────────────────────────────────────────────

    [Fact]
    public void ToDisplayText_WithAllFields_FormatsCorrectly()
    {
        var result = new BrainAnalysisResult
        {
            VisualDescription = "White pieces dominating center",
            PositionAssessment = "White is better with strong center control",
            Threats = "Knight fork on f7",
            SuggestedAction = "Play Nf3 to defend",
            Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"
        };

        var text = result.ToDisplayText();

        text.Should().Contain("White is better");
        text.Should().Contain("Threats: Knight fork");
        text.Should().Contain("Suggestion: Play Nf3");
        text.Should().Contain("[FEN:");
        // visual_description is NOT in display text (it's internal CoT grounding)
        text.Should().NotContain("White pieces dominating center");
    }

    [Fact]
    public void ToDisplayText_WithMinimalFields_HandlesNulls()
    {
        var result = new BrainAnalysisResult
        {
            PositionAssessment = "Position is equal"
        };

        var text = result.ToDisplayText();

        text.Should().Be("Position is equal");
    }

    [Fact]
    public void ToDisplayText_WithNoFields_ReturnsFallback()
    {
        var result = new BrainAnalysisResult();
        result.ToDisplayText().Should().Be("Analysis complete (no details).");
    }

    // ── ConfidenceScore ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("CERTAIN", 0.95)]
    [InlineData("LIKELY", 0.85)]
    [InlineData("UNCERTAIN", 0.6)]
    [InlineData("GUESSING", 0.3)]
    [InlineData("certain", 0.95)]
    [InlineData(null, 0.5)]
    [InlineData("UNKNOWN", 0.5)]
    public void ConfidenceScore_MapsCorrectly(string? tag, double expected)
    {
        var result = new BrainAnalysisResult { Confidence = tag };
        result.ConfidenceScore.Should().Be(expected);
    }

    // ── TryParseStructuredAnalysis (static method on BrainEventRouter) ───────

    [Fact]
    public void TryParseStructuredAnalysis_ValidJson_ReturnsParsed()
    {
        var json = """{"visual_description":"Board shows e4","position_assessment":"Equal","confidence":"LIKELY","fen":"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"}""";

        var result = BrainEventRouter.TryParseStructuredAnalysis(json);

        result.Should().NotBeNull();
        result!.VisualDescription.Should().Be("Board shows e4");
        result.PositionAssessment.Should().Be("Equal");
        result.Confidence.Should().Be("LIKELY");
        result.Fen.Should().Contain("rnbqkbnr");
    }

    [Fact]
    public void TryParseStructuredAnalysis_InvalidJson_ReturnsNull()
    {
        var result = BrainEventRouter.TryParseStructuredAnalysis("{not valid json{{{");
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseStructuredAnalysis_FreeText_ReturnsNull()
    {
        var result = BrainEventRouter.TryParseStructuredAnalysis("The position shows a strong center for White with pawns on e4 and d4.");
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseStructuredAnalysis_PartialJson_ReturnsPartial()
    {
        var json = """{"visual_description":"Board shows pieces","position_assessment":"White is better","confidence":"CERTAIN"}""";

        var result = BrainEventRouter.TryParseStructuredAnalysis(json);

        result.Should().NotBeNull();
        result!.VisualDescription.Should().Be("Board shows pieces");
        result.Fen.Should().BeNull();
        result.LastMove.Should().BeNull();
    }

    [Fact]
    public void TryParseStructuredAnalysis_EmptyString_ReturnsNull()
    {
        BrainEventRouter.TryParseStructuredAnalysis("").Should().BeNull();
        BrainEventRouter.TryParseStructuredAnalysis("   ").Should().BeNull();
    }
}
