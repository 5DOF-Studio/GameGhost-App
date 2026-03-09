using GaimerDesktop.Models;

namespace GaimerDesktop.Tests.Session;

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
    public void GetBestMove_ParametersSchema_IsEmptyObject()
    {
        var schema = ToolDefinitions.GetBestMove.ParametersSchema;

        schema.Should().Contain("\"properties\"");
        using var doc = JsonDocument.Parse(schema);
        doc.RootElement.GetProperty("properties").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void AllToolDefinitions_RequiresInGameFlags_Correct()
    {
        var allTools = new[]
        {
            ToolDefinitions.WebSearch,
            ToolDefinitions.PlayerHistory,
            ToolDefinitions.PlayerAnalytics,
            ToolDefinitions.CaptureScreen,
            ToolDefinitions.GetBestMove,
            ToolDefinitions.GetGameState,
            ToolDefinitions.AnalyzePositionEngine,
            ToolDefinitions.AnalyzePositionStrategic,
        };

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
}
