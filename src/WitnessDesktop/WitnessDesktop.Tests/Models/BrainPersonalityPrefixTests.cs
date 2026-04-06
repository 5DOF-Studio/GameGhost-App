using FluentAssertions;
using WitnessDesktop.Models;
using Xunit;

namespace WitnessDesktop.Tests.Models;

/// <summary>
/// Verifies that brain personality prefixes establish vision identity,
/// tool awareness, capture cadence, illusion language rules, and FEN extraction.
/// The agent must speak as if watching the board live — never reference screenshots or images.
/// </summary>
public class BrainPersonalityPrefixTests
{
    private readonly string _leroyPrefix = Agents.Chess.BrainPersonalityPrefix!;
    private readonly string _waspPrefix = Agents.Wasp.BrainPersonalityPrefix!;

    // ── Vision Identity ──────────────────────────────────────────────────────

    [Fact]
    public void Leroy_BrainPrefix_ContainsVisionSection()
    {
        _leroyPrefix.Should().Contain("[VISION]");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsVisionSection()
    {
        _waspPrefix.Should().Contain("[VISION]");
    }

    [Fact]
    public void Leroy_BrainPrefix_ContainsIdentitySection()
    {
        _leroyPrefix.Should().Contain("[IDENTITY]");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsIdentitySection()
    {
        _waspPrefix.Should().Contain("[IDENTITY]");
    }

    [Fact]
    public void Leroy_BrainPrefix_ContainsLanguageRulesSection()
    {
        _leroyPrefix.Should().Contain("[LANGUAGE RULES");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsLanguageRulesSection()
    {
        _waspPrefix.Should().Contain("[LANGUAGE RULES");
    }

    // ── Illusion Language (NEVER say) ────────────────────────────────────────

    [Fact]
    public void Leroy_BrainPrefix_ContainsNeverSayRules()
    {
        _leroyPrefix.Should().Contain("NEVER say");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsNeverSayRules()
    {
        _waspPrefix.Should().Contain("NEVER say");
    }

    [Fact]
    public void Leroy_BrainPrefix_ForbidsScreenshotReference()
    {
        _leroyPrefix.Should().Contain("I see an image");
    }

    [Fact]
    public void Wasp_BrainPrefix_ForbidsScreenshotReference()
    {
        _waspPrefix.Should().Contain("I see an image");
    }

    [Fact]
    public void Leroy_BrainPrefix_ForbidsCaptureReference()
    {
        _leroyPrefix.Should().Contain("from the capture");
    }

    [Fact]
    public void Wasp_BrainPrefix_ForbidsCaptureReference()
    {
        _waspPrefix.Should().Contain("from the capture");
    }

    [Fact]
    public void Leroy_BrainPrefix_ForbidsAnalyzingScreenshot()
    {
        _leroyPrefix.Should().Contain("analyzing the screenshot");
    }

    [Fact]
    public void Wasp_BrainPrefix_ForbidsAnalyzingScreenshot()
    {
        _waspPrefix.Should().Contain("analyzing the screenshot");
    }

    // ── Tool Awareness ───────────────────────────────────────────────────────

    [Fact]
    public void Leroy_BrainPrefix_ContainsCaptureScreenTool()
    {
        _leroyPrefix.Should().Contain("capture_screen");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsCaptureScreenTool()
    {
        _waspPrefix.Should().Contain("capture_screen");
    }

    [Fact]
    public void Leroy_BrainPrefix_ContainsAnalyzePositionEngineTool()
    {
        _leroyPrefix.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsAnalyzePositionEngineTool()
    {
        _waspPrefix.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Leroy_BrainPrefix_ContainsGameJournalTool()
    {
        _leroyPrefix.Should().Contain("game_journal");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsGameJournalTool()
    {
        _waspPrefix.Should().Contain("game_journal");
    }

    // ── Capture Cadence ──────────────────────────────────────────────────────

    [Fact]
    public void Leroy_BrainPrefix_ContainsCaptureCadence()
    {
        _leroyPrefix.Should().Contain("whenever the board changes");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsCaptureCadence()
    {
        _waspPrefix.Should().Contain("whenever the board changes");
    }

    // ── FEN Extraction ───────────────────────────────────────────────────────

    [Fact]
    public void Leroy_BrainPrefix_ContainsFenInstruction()
    {
        _leroyPrefix.Should().Contain("[FEN:");
    }

    [Fact]
    public void Wasp_BrainPrefix_ContainsFenInstruction()
    {
        _waspPrefix.Should().Contain("[FEN:");
    }

    // ── Illusion Integrity ───────────────────────────────────────────────────
    // The word "screenshot" should ONLY appear inside the NEVER rules list,
    // not as something the agent says or references positively.

    [Theory]
    [InlineData("Leroy")]
    [InlineData("Wasp")]
    public void BrainPrefix_ScreenshotOnlyInNeverList(string agentName)
    {
        var prefix = agentName == "Leroy" ? _leroyPrefix : _waspPrefix;

        // Find all occurrences of "screenshot" — they must be in NEVER rules context
        var lines = prefix.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("screenshot", System.StringComparison.OrdinalIgnoreCase))
            {
                // The line must be part of the NEVER rules (contains "NEVER" or is a list item under it)
                var trimmed = line.TrimStart();
                var isNeverRule = trimmed.StartsWith("- NEVER", System.StringComparison.OrdinalIgnoreCase)
                    || trimmed.Contains("NEVER say", System.StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("- ", System.StringComparison.Ordinal);
                isNeverRule.Should().BeTrue(
                    $"'{agentName}' prefix mentions 'screenshot' outside NEVER rules: \"{line.Trim()}\"");
            }
        }
    }

    // ── Minimum Length ───────────────────────────────────────────────────────

    [Fact]
    public void Leroy_BrainPrefix_IsSubstantial()
    {
        _leroyPrefix.Length.Should().BeGreaterThan(200,
            "BrainPersonalityPrefix should be at least 200 chars (not too thin)");
    }

    [Fact]
    public void Wasp_BrainPrefix_IsSubstantial()
    {
        _waspPrefix.Length.Should().BeGreaterThan(200,
            "BrainPersonalityPrefix should be at least 200 chars (not too thin)");
    }
}
