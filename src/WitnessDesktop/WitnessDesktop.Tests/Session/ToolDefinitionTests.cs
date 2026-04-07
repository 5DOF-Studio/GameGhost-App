using WitnessDesktop.Models;

namespace WitnessDesktop.Tests.Session;

public class ToolDefinitionTests
{
    [Fact]
    public void WebSearch_ParametersSchema_HasQueryField()
    {
        var schema = ToolDefinitions.WebSearch.ParametersSchema;

        schema.Should().Contain("\"query\"");
        schema.Should().Contain("\"required\"");
        using var doc = JsonDocument.Parse(schema);
        doc.RootElement.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void AllToolDefinitions_RequiresInGameFlags_Correct()
    {
        var allTools = ToolDefinitions.All;

        allTools.Count(t => !t.RequiresInGame).Should().Be(3);
        allTools.Count(t => t.RequiresInGame).Should().Be(5);
    }

    [Fact]
    public void AnalyzePositionEngine_ParametersSchema_HasFenRequired()
    {
        var schema = ToolDefinitions.AnalyzePositionEngine.ParametersSchema;

        schema.Should().Contain("\"fen\"");
        schema.Should().Contain("\"required\"");
        using var doc = JsonDocument.Parse(schema);
        doc.RootElement.GetProperty("type").GetString().Should().Be("object");
        doc.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("fen");
    }

    [Fact]
    public void AnalyzePositionStrategic_ParametersSchema_HasFocusAndColor()
    {
        var schema = ToolDefinitions.AnalyzePositionStrategic.ParametersSchema;

        schema.Should().Contain("\"focus\"");
        schema.Should().Contain("\"player_color\"");
        using var doc = JsonDocument.Parse(schema);
        doc.RootElement.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void AnalyzePositionEngine_RequiresInGame()
    {
        ToolDefinitions.AnalyzePositionEngine.RequiresInGame.Should().BeTrue();
    }

    [Fact]
    public void AnalyzePositionStrategic_RequiresInGame()
    {
        ToolDefinitions.AnalyzePositionStrategic.RequiresInGame.Should().BeTrue();
    }

    [Fact]
    public void AllToolDefinitions_HaveDisplayNameIconAndActionLabel()
    {
        ToolDefinitions.All.Should().OnlyContain(tool =>
            !string.IsNullOrWhiteSpace(tool.DisplayName) &&
            !string.IsNullOrWhiteSpace(tool.Icon) &&
            !string.IsNullOrWhiteSpace(tool.ActionLabel));
    }

    [Fact]
    public void FindByName_KnownTool_ReturnsDefinition()
    {
        var result = ToolDefinitions.FindByName("game_journal");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Game Journal");
        result.Icon.Should().Be("history_clock.png");
        result.ActionLabel.Should().Be("Updating journal");
    }

    [Fact]
    public void FindByName_UnknownTool_ReturnsNull()
    {
        ToolDefinitions.FindByName("missing_tool").Should().BeNull();
    }

    [Fact]
    public void ToolCallInfo_UsesToolDefinitionDisplayMetadata()
    {
        var info = new ToolCallInfo { ToolName = "analyze_position_engine", Success = true, DurationMs = 812 };

        info.DisplayName.Should().Be("Engine Analysis");
        info.Icon.Should().Be("tool_engine.png");
        info.ActionLabel.Should().Be("Ran engine analysis");
        info.SummaryText.Should().Be("Ran engine analysis");
        info.DurationLabel.Should().Be("812ms");
        info.HasDuration.Should().BeTrue();
    }

    [Fact]
    public void ToolCallInfo_UnknownTool_FallsBackToRawNameAndGenericIcon()
    {
        var info = new ToolCallInfo { ToolName = "custom_tool", Success = true };

        info.DisplayName.Should().Be("custom_tool");
        info.Icon.Should().Be("tool_generic.png");
        info.ActionLabel.Should().Be("custom_tool");
        info.SummaryText.Should().Be("custom_tool");
    }

    [Fact]
    public void ToolCallInfo_FailedTool_UsesFailureSummary()
    {
        var info = new ToolCallInfo { ToolName = "web_search", Success = false };

        info.SummaryText.Should().Be("Web Search failed");
    }

    [Fact]
    public void ToolCallInfo_ZeroDuration_HasEmptyDurationLabel()
    {
        var info = new ToolCallInfo { ToolName = "game_journal", Success = true, DurationMs = 0 };

        info.DurationLabel.Should().BeEmpty();
        info.HasDuration.Should().BeFalse();
    }
}
