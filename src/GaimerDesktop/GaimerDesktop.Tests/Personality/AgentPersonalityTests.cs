using GaimerDesktop.Models;
using GaimerDesktop.Services;

namespace GaimerDesktop.Tests.Personality;

public class AgentPersonalityTests
{
    // ── ComposedPersonality Composition ──────────────────────────────────────

    [Fact]
    public void ComposedPersonality_WithAllSections_ConcatenatesInOrder()
    {
        var agent = new Agent
        {
            Key = "test", Id = "Test", Name = "Test", PrimaryGame = "Test",
            IconImage = "test.png", PortraitImage = "test.png",
            Description = "Test", Features = ["Test"], Type = AgentType.General,
            SystemInstruction = "Legacy instruction",
            SoulBlock = "Test soul",
            StyleBlock = "Test style",
            BehaviorBlock = "Test behavior",
            SituationsBlock = "Test situations",
            AntiPatternsBlock = "Test anti-patterns",
        };

        var result = agent.ComposedPersonality;

        result.Should().Contain("[WHO YOU ARE]");
        result.Should().Contain("Test soul");
        result.Should().Contain("[HOW YOU TALK]");
        result.Should().Contain("Test style");
        result.Should().Contain("[HOW YOU BEHAVE]");
        result.Should().Contain("Test behavior");
        result.Should().Contain("[SITUATIONAL MODES]");
        result.Should().Contain("Test situations");
        result.Should().Contain("[NEVER DO]");
        result.Should().Contain("Test anti-patterns");
    }

    [Fact]
    public void ComposedPersonality_SectionsAppearInCorrectOrder()
    {
        var agent = new Agent
        {
            Key = "test", Id = "Test", Name = "Test", PrimaryGame = "Test",
            IconImage = "test.png", PortraitImage = "test.png",
            Description = "Test", Features = ["Test"], Type = AgentType.General,
            SystemInstruction = "Legacy",
            SoulBlock = "soul", StyleBlock = "style", BehaviorBlock = "behavior",
            SituationsBlock = "situations", AntiPatternsBlock = "anti",
        };

        var result = agent.ComposedPersonality;

        var soulIdx = result.IndexOf("[WHO YOU ARE]");
        var styleIdx = result.IndexOf("[HOW YOU TALK]");
        var behaviorIdx = result.IndexOf("[HOW YOU BEHAVE]");
        var situationsIdx = result.IndexOf("[SITUATIONAL MODES]");
        var antiIdx = result.IndexOf("[NEVER DO]");

        soulIdx.Should().BeLessThan(styleIdx);
        styleIdx.Should().BeLessThan(behaviorIdx);
        behaviorIdx.Should().BeLessThan(situationsIdx);
        situationsIdx.Should().BeLessThan(antiIdx);
    }

    [Fact]
    public void ComposedPersonality_NoSections_FallsBackToSystemInstruction()
    {
        var agent = new Agent
        {
            Key = "test", Id = "Test", Name = "Test", PrimaryGame = "Test",
            IconImage = "test.png", PortraitImage = "test.png",
            Description = "Test", Features = ["Test"], Type = AgentType.General,
            SystemInstruction = "Legacy system instruction content",
        };

        agent.ComposedPersonality.Should().Be("Legacy system instruction content");
    }

    [Fact]
    public void ComposedPersonality_PartialSections_IncludesOnlyPopulated()
    {
        var agent = new Agent
        {
            Key = "test", Id = "Test", Name = "Test", PrimaryGame = "Test",
            IconImage = "test.png", PortraitImage = "test.png",
            Description = "Test", Features = ["Test"], Type = AgentType.General,
            SystemInstruction = "Legacy",
            SoulBlock = "Test soul",
            BehaviorBlock = "Test behavior",
        };

        var result = agent.ComposedPersonality;

        result.Should().Contain("[WHO YOU ARE]");
        result.Should().Contain("[HOW YOU BEHAVE]");
        result.Should().NotContain("[HOW YOU TALK]");
        result.Should().NotContain("[SITUATIONAL MODES]");
        result.Should().NotContain("[NEVER DO]");
    }

    // ── Chess Agent Personality Sections ─────────────────────────────────────

    [Fact]
    public void Leroy_HasAllPersonalitySections()
    {
        var leroy = Agents.Chess;

        leroy.SoulBlock.Should().NotBeNullOrEmpty();
        leroy.StyleBlock.Should().NotBeNullOrEmpty();
        leroy.BehaviorBlock.Should().NotBeNullOrEmpty();
        leroy.SituationsBlock.Should().NotBeNullOrEmpty();
        leroy.AntiPatternsBlock.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Wasp_HasAllPersonalitySections()
    {
        var wasp = Agents.Wasp;

        wasp.SoulBlock.Should().NotBeNullOrEmpty();
        wasp.StyleBlock.Should().NotBeNullOrEmpty();
        wasp.BehaviorBlock.Should().NotBeNullOrEmpty();
        wasp.SituationsBlock.Should().NotBeNullOrEmpty();
        wasp.AntiPatternsBlock.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Leroy_HasBrainPersonalityPrefix()
    {
        Agents.Chess.BrainPersonalityPrefix.Should().NotBeNullOrEmpty();
        Agents.Chess.BrainPersonalityPrefix.Should().Contain("Leroy");
    }

    [Fact]
    public void Wasp_HasBrainPersonalityPrefix()
    {
        Agents.Wasp.BrainPersonalityPrefix.Should().NotBeNullOrEmpty();
        Agents.Wasp.BrainPersonalityPrefix.Should().Contain("Wasp");
    }

    [Fact]
    public void Derek_HasNoPersonalitySections_FallsBackToSystemInstruction()
    {
        var derek = Agents.General;

        derek.SoulBlock.Should().BeNull();
        derek.ComposedPersonality.Should().Be(derek.SystemInstruction);
    }

    [Fact]
    public void Leroy_ComposedPersonality_ContainsAllSectionHeaders()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("[WHO YOU ARE]");
        result.Should().Contain("[HOW YOU TALK]");
        result.Should().Contain("[HOW YOU BEHAVE]");
        result.Should().Contain("[SITUATIONAL MODES]");
        result.Should().Contain("[NEVER DO]");
    }

    [Fact]
    public void Leroy_ComposedPersonality_ContainsSignatureContent()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("Leroy");
        result.Should().Contain("knight");
        result.Should().Contain("boss");
    }

    [Fact]
    public void Wasp_ComposedPersonality_ContainsSignatureContent()
    {
        var result = Agents.Wasp.ComposedPersonality;

        result.Should().Contain("Wasp");
        result.Should().Contain("Control the board");
    }

    // ── Personality Distinctiveness ──────────────────────────────────────────

    [Fact]
    public void Leroy_And_Wasp_HaveDistinctSouls()
    {
        Agents.Chess.SoulBlock.Should().NotBe(Agents.Wasp.SoulBlock);
        Agents.Chess.SoulBlock.Should().Contain("Leroy");
        Agents.Wasp.SoulBlock.Should().Contain("Wasp");
    }

    [Fact]
    public void Leroy_And_Wasp_HaveDistinctStyles()
    {
        Agents.Chess.StyleBlock.Should().NotBe(Agents.Wasp.StyleBlock);
        // Leroy uses "boss", "showtime" -- Wasp never does
        Agents.Chess.StyleBlock.Should().Contain("boss");
        Agents.Wasp.AntiPatternsBlock.Should().Contain("boss");
    }

    [Fact]
    public void Leroy_And_Wasp_HaveDistinctBrainPrefixes()
    {
        Agents.Chess.BrainPersonalityPrefix.Should().NotBe(Agents.Wasp.BrainPersonalityPrefix);
    }

    // ── ToolGuidanceBlock in ComposedPersonality ──────────────────────────

    [Fact]
    public void Leroy_ComposedPersonality_ContainsChessToolGuidance()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("CHESS TOOLS:");
        result.Should().Contain("analyze_position_engine");
        result.Should().Contain("FEN EXTRACTION");
        result.Should().Contain("VOICE OUTPUT");
    }

    [Fact]
    public void Wasp_ComposedPersonality_ContainsChessToolGuidance()
    {
        var result = Agents.Wasp.ComposedPersonality;

        result.Should().Contain("CHESS TOOLS:");
        result.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Derek_ComposedPersonality_DoesNotContainChessToolGuidance()
    {
        var result = Agents.General.ComposedPersonality;

        result.Should().NotContain("CHESS TOOLS:");
    }

    // ── SystemInstruction Backward Compatibility ────────────────────────────

    [Fact]
    public void Leroy_SystemInstruction_StillContainsChessToolGuidance()
    {
        Agents.Chess.SystemInstruction.Should().Contain("CHESS TOOLS:");
        Agents.Chess.SystemInstruction.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Wasp_SystemInstruction_StillContainsChessToolGuidance()
    {
        Agents.Wasp.SystemInstruction.Should().Contain("CHESS TOOLS:");
        Agents.Wasp.SystemInstruction.Should().Contain("analyze_position_engine");
    }

    // ── AgentKey on SessionContext ───────────────────────────────────────────

    [Fact]
    public void SessionContext_AgentKey_DefaultsToNull()
    {
        var ctx = new SessionContext();
        ctx.AgentKey.Should().BeNull();
    }

    [Fact]
    public void SessionContext_AgentKey_CanBeSet()
    {
        var ctx = new SessionContext { AgentKey = "chess" };
        ctx.AgentKey.Should().Be("chess");
    }
}

public class ChatPromptBuilderAgentAwarenessTests
{
    private ChatPromptBuilder CreateSut() => new();

    private static SessionContext OutGameSession() => new()
    {
        State = SessionState.OutGame,
        UserName = "TestPlayer",
        UserTier = "Champion",
    };

    private static IReadOnlyList<ToolDefinition> SampleTools() => new List<ToolDefinition>
    {
        ToolDefinitions.WebSearch,
    };

    [Fact]
    public void BuildSystemPrompt_WithLeroyAgent_ContainsLeroyIdentity()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.Chess);

        result.Should().Contain("Leroy");
        result.Should().NotContain("Dross");
    }

    [Fact]
    public void BuildSystemPrompt_WithWaspAgent_ContainsWaspIdentity()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.Wasp);

        result.Should().Contain("Wasp");
        result.Should().NotContain("Dross");
    }

    [Fact]
    public void BuildSystemPrompt_WithNullAgent_FallsBackToDross()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), null);

        result.Should().Contain("Dross");
    }

    [Fact]
    public void BuildSystemPrompt_WithNoAgentParam_FallsBackToDross()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools());

        result.Should().Contain("Dross");
    }

    [Fact]
    public void BuildSystemPrompt_WithAgent_ContainsTextMediumRules()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.Chess);

        result.Should().Contain("text chat");
    }

    [Fact]
    public void BuildSystemPrompt_WithAgent_ContainsBehaviorBlock()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.Chess);

        // Agent's BehaviorBlock content should be present (from BuildAgentIdentity)
        result.Should().Contain("Priority order");
    }

    [Fact]
    public void BuildSystemPrompt_WithDerek_NoSections_FallsBackToDross()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.General);

        // Derek has no SoulBlock, so falls back to CoreIdentity (Dross)
        result.Should().Contain("Dross");
    }

    [Fact]
    public void BuildSystemPrompt_WithAgent_DoesNotContainVoiceStyleBlock()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.Chess);

        // Voice STYLE block headers should NOT appear in text chat (only SOUL + BEHAVIOR)
        result.Should().NotContain("[HOW YOU TALK]");
        result.Should().NotContain("[SITUATIONAL MODES]");
        result.Should().NotContain("[NEVER DO]");
    }
}
