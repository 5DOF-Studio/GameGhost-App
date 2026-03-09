using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Prompts;

public class ChatPromptBuilderTests
{
    private ChatPromptBuilder CreateSut() => new();

    private static SessionContext OutGameSession() => new()
    {
        State = SessionState.OutGame,
        UserName = "TestPlayer",
        UserTier = "Champion",
    };

    private static SessionContext InGameSession() => new()
    {
        State = SessionState.InGame,
        GameId = "game-1",
        GameType = "chess",
        ConnectorName = "lichess",
        GameStartedAt = DateTime.UtcNow.AddMinutes(-5),
        UserName = "TestPlayer",
        UserTier = "Champion",
    };

    private static IReadOnlyList<ToolDefinition> SampleTools() => new List<ToolDefinition>
    {
        ToolDefinitions.WebSearch,
        ToolDefinitions.PlayerHistory,
        ToolDefinitions.CaptureScreen,
    };

    // ── Core Identity ───────────────────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_ContainsCoreIdentity()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools());

        result.Should().Contain("Dross", "prompt must establish agent identity");
        result.Should().Contain("Gaimer", "prompt must reference the product name");
        result.Should().Contain("copilot", "prompt must describe the agent role");
    }

    // ── Behavior Rules ──────────────────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_ContainsBehaviorRules()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools());

        result.Should().Contain("THINGS TO NEVER DO", "prompt must include explicit prohibition section");
        result.Should().Contain("Never say", "prompt must have concrete prohibitions");
    }

    [Fact]
    public void BuildSystemPrompt_InGame_ContainsInGameBehavior()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(InGameSession(), SampleTools());

        result.Should().Contain("IN-GAME BEHAVIOR", "prompt must have in-game section header");
        result.Should().Contain("call tools before responding", "in-game prompt must instruct tool-first pattern");
        result.Should().NotContain("OUT-GAME BEHAVIOR", "in-game prompt must not include out-game rules");
    }

    [Fact]
    public void BuildSystemPrompt_OutGame_ContainsOutGameBehavior()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools());

        result.Should().Contain("OUT-GAME BEHAVIOR", "prompt must have out-game section header");
        result.Should().Contain("conversational", "out-game prompt should set relaxed tone");
        result.Should().NotContain("IN-GAME BEHAVIOR", "out-game prompt must not include in-game rules");
    }

    // ── Session Context Block ───────────────────────────────────────────────

    [Fact]
    public void BuildSessionContextBlock_InGame_IncludesAllGameFields()
    {
        var sut = CreateSut();
        var session = InGameSession();

        var result = sut.BuildSessionContextBlock(session);

        result.Should().Contain("[SESSION CONTEXT]", "context block must have section header");
        result.Should().Contain("In-game", "must indicate in-game state");
        result.Should().Contain("chess", "must include game type");
        result.Should().Contain("lichess", "must include connector name");
        result.Should().Contain("TestPlayer", "must include player name");
        result.Should().Contain("Active", "must indicate brain pipeline is active");
    }

    [Fact]
    public void BuildSessionContextBlock_OutGame_IncludesPlayerAndIdleState()
    {
        var sut = CreateSut();
        var session = OutGameSession();

        var result = sut.BuildSessionContextBlock(session);

        result.Should().Contain("[SESSION CONTEXT]", "context block must have section header");
        result.Should().Contain("Out-game", "must indicate out-game state");
        result.Should().Contain("TestPlayer", "must include player name");
        result.Should().Contain("Champion", "must include player tier");
        result.Should().Contain("Idle", "must indicate brain pipeline is idle");
    }

    // ── Tool Listing ────────────────────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_ListsAllToolNamesAndDescriptions()
    {
        var sut = CreateSut();
        var tools = SampleTools();

        var result = sut.BuildSystemPrompt(OutGameSession(), tools);

        result.Should().Contain("[AVAILABLE TOOLS]", "prompt must have tools section header");
        foreach (var tool in tools)
        {
            result.Should().Contain(tool.Name, $"prompt should list tool '{tool.Name}'");
            result.Should().Contain(tool.Description, $"prompt should include description for '{tool.Name}'");
        }
    }

    [Fact]
    public void BuildSystemPrompt_EmptyTools_ShowsNoToolsMessage()
    {
        var sut = CreateSut();
        var tools = new List<ToolDefinition>();

        var result = sut.BuildSystemPrompt(OutGameSession(), tools);

        result.Should().Contain("No tools available", "empty tool list should show explicit message");
    }

    // ── Text Medium Rules ───────────────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_ContainsTextMediumRules()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools());

        result.Should().Contain("text chat", "prompt must specify text medium context");
        result.Should().Contain("No emoji", "prompt must have emoji restriction");
    }

    // ── Structural Completeness ─────────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_HasAllFourSections()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(InGameSession(), SampleTools());

        // Verify all four sections are present in order
        var identityIdx = result.IndexOf("Dross");
        var textRulesIdx = result.IndexOf("text chat");
        var contextIdx = result.IndexOf("[SESSION CONTEXT]");
        var toolsIdx = result.IndexOf("[AVAILABLE TOOLS]");
        var behaviorIdx = result.IndexOf("THINGS TO NEVER DO");

        identityIdx.Should().BeGreaterOrEqualTo(0, "identity section missing");
        textRulesIdx.Should().BeGreaterThan(identityIdx, "text rules should follow identity");
        contextIdx.Should().BeGreaterThan(textRulesIdx, "session context should follow text rules");
        toolsIdx.Should().BeGreaterThan(contextIdx, "tools should follow session context");
        behaviorIdx.Should().BeGreaterThan(toolsIdx, "behavior rules should follow tools");
    }
}
