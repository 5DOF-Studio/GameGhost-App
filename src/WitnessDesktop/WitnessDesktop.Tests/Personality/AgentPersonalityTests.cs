using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Personality;

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
    public void Rasa_IsAvailable()
    {
        var rasa = Agents.General;
        Assert.Equal("RASA", rasa.Name);
        Assert.True(rasa.IsAvailable);
        Assert.Equal(AgentType.General, rasa.Type);
    }

    [Fact]
    public void Rasa_HasAllFivePersonalityBlocks()
    {
        var rasa = Agents.General;
        Assert.NotNull(rasa.SoulBlock);
        Assert.NotNull(rasa.StyleBlock);
        Assert.NotNull(rasa.BehaviorBlock);
        Assert.NotNull(rasa.SituationsBlock);
        Assert.NotNull(rasa.AntiPatternsBlock);
    }

    [Fact]
    public void Rasa_HasBrainPersonalityPrefix()
    {
        var rasa = Agents.General;
        Assert.NotNull(rasa.BrainPersonalityPrefix);
        Assert.Contains("[IDENTITY]", rasa.BrainPersonalityPrefix);
        Assert.Contains("[VISION]", rasa.BrainPersonalityPrefix);
        Assert.Contains("[LANGUAGE RULES", rasa.BrainPersonalityPrefix);
    }

    [Fact]
    public void Rasa_HasToolGuidanceBlock()
    {
        var rasa = Agents.General;
        Assert.NotNull(rasa.ToolGuidanceBlock);
        Assert.Contains("capture_screen", rasa.ToolGuidanceBlock);
        Assert.Contains("game_journal", rasa.ToolGuidanceBlock);
        Assert.Contains("web_search", rasa.ToolGuidanceBlock);
    }

    [Fact]
    public void Rasa_ComposedPersonality_ContainsAllSections()
    {
        var rasa = Agents.General;
        var composed = rasa.ComposedPersonality;
        Assert.Contains("[WHO YOU ARE]", composed);
        Assert.Contains("[HOW YOU TALK]", composed);
        Assert.Contains("[HOW YOU BEHAVE]", composed);
        Assert.Contains("[SITUATIONAL MODES]", composed);
        Assert.Contains("[NEVER DO]", composed);
        Assert.Contains("[LIVE BOARD CONTRACT]", composed);
    }

    [Fact]
    public void Rasa_IsDistinctFromLeroy()
    {
        var rasa = Agents.General;
        var leroy = Agents.Chess;
        Assert.NotEqual(rasa.SoulBlock, leroy.SoulBlock);
        Assert.NotEqual(rasa.StyleBlock, leroy.StyleBlock);
        Assert.NotEqual(rasa.Name, leroy.Name);
    }

    [Fact]
    public void Rasa_HasDistinctVoiceId()
    {
        var rasa = Agents.General;
        Assert.Equal("echo", rasa.VoiceId);
        Assert.Equal("male", rasa.VoiceGender);
    }

    [Fact]
    public void Rasa_CaptureConfig_DisablesDiffGating()
    {
        var rasa = Agents.General;
        Assert.Equal(255, rasa.CaptureConfig.DiffThreshold);
        Assert.Equal(9, rasa.CaptureConfig.DiffHashWidth);
        Assert.Equal(5000, rasa.CaptureConfig.CaptureIntervalMs);
    }

    [Fact]
    public void Rasa_Tools_AreGameAgnostic()
    {
        var rasa = Agents.General;
        Assert.NotNull(rasa.Tools);
        Assert.Contains("capture_screen", rasa.Tools);
        Assert.Contains("web_search", rasa.Tools);
        Assert.Contains("game_journal", rasa.Tools);
        Assert.DoesNotContain("analyze_position_engine", rasa.Tools);
        Assert.DoesNotContain("analyze_position_strategic", rasa.Tools);
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
    public void Leroy_ComposedPersonality_ContainsLiveBoardContract()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("[LIVE BOARD CONTRACT]");
        result.Should().Contain("NEVER say");
        result.Should().Contain("I can't see the board");
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
    public void Leroy_ComposedPersonality_ContainsCoachingResponsePattern()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("COACHING RESPONSE PATTERN:");
        result.Should().Contain("You played [move], they answered with [move].");
        result.Should().Contain("If the move sequence is uncertain, skip the recap");
    }

    [Fact]
    public void Leroy_ComposedPersonality_ContainsSpokenNaturalnessGuidance()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("SPOKEN NATURALNESS:");
        result.Should().Contain("\"hmm\", \"oh\", \"ooh\", \"ah\", \"alright\", or \"okay\"");
        result.Should().Contain("Never stack them");
    }

    [Fact]
    public void Leroy_ComposedPersonality_ContainsCheckDangerResponsePattern()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("CHECK / MATE DANGER RESPONSE PATTERN:");
        result.Should().Contain("You're in check.");
        result.Should().Contain("If check status is uncertain, do not declare check as fact.");
        result.Should().Contain("best practical resistance");
    }

    [Fact]
    public void Wasp_ComposedPersonality_ContainsChessToolGuidance()
    {
        var result = Agents.Wasp.ComposedPersonality;

        result.Should().Contain("CHESS TOOLS:");
        result.Should().Contain("analyze_position_engine");
    }

    [Fact]
    public void Wasp_ComposedPersonality_ContainsCoachingResponsePattern()
    {
        var result = Agents.Wasp.ComposedPersonality;

        result.Should().Contain("COACHING RESPONSE PATTERN:");
        result.Should().Contain("You played [move], they answered with [move].");
    }

    [Fact]
    public void Wasp_ComposedPersonality_ContainsCheckDangerResponsePattern()
    {
        var result = Agents.Wasp.ComposedPersonality;

        result.Should().Contain("CHECK / MATE DANGER RESPONSE PATTERN:");
        result.Should().Contain("You're in check.");
        result.Should().Contain("This looks dangerous");
    }

    [Fact]
    public void ChessToolGuidance_ContainsMoveRecapIntegrityRule()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("MOVE RECAP INTEGRITY:");
        result.Should().Contain("Never fabricate the player's last move");
    }

    [Fact]
    public void Rasa_ComposedPersonality_ContainsLiveBoardContract()
    {
        var result = Agents.General.ComposedPersonality;

        result.Should().Contain("[LIVE BOARD CONTRACT]");
        result.Should().Contain("LANGUAGE RULES");
    }

    [Fact]
    public void Rasa_ComposedPersonality_DoesNotContainChessToolGuidance()
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

    // ── Voice Discipline Block ──────────────────────────────────────────────

    [Fact]
    public void ComposedPersonality_ContainsVoiceDisciplineBlock()
    {
        var result = Agents.Chess.ComposedPersonality;

        result.Should().Contain("[IN-GAME VOICE DISCIPLINE]");
        result.Should().Contain("NEVER end your response with a question");
        result.Should().Contain("Statements, not questions");
        result.Should().Contain("FORBIDDEN closers");
    }

    [Fact]
    public void VoiceDiscipline_AppearsBeforeSoulBlock()
    {
        var result = Agents.Chess.ComposedPersonality;

        var disciplineIdx = result.IndexOf("[IN-GAME VOICE DISCIPLINE]");
        var soulIdx = result.IndexOf("[WHO YOU ARE]");

        disciplineIdx.Should().BeGreaterThan(-1);
        soulIdx.Should().BeGreaterThan(-1);
        disciplineIdx.Should().BeLessThan(soulIdx);
    }

    [Fact]
    public void VoiceDiscipline_PresentOnAllAgents()
    {
        Agents.Chess.ComposedPersonality.Should().Contain("[IN-GAME VOICE DISCIPLINE]");
        Agents.Wasp.ComposedPersonality.Should().Contain("[IN-GAME VOICE DISCIPLINE]");
        Agents.General.ComposedPersonality.Should().Contain("[IN-GAME VOICE DISCIPLINE]");
    }

    // ── No Follow-Up Question Anti-Patterns ─────────────────────────────────

    [Fact]
    public void Leroy_AntiPatterns_ForbidsFollowUpQuestions()
    {
        Agents.Chess.AntiPatternsBlock.Should().Contain("NEVER end your response with a follow-up question");
        Agents.Chess.AntiPatternsBlock.Should().Contain("want me to explain?");
    }

    [Fact]
    public void Wasp_AntiPatterns_ForbidsFollowUpQuestions()
    {
        Agents.Wasp.AntiPatternsBlock.Should().Contain("NEVER end your response with a follow-up question");
        Agents.Wasp.AntiPatternsBlock.Should().Contain("want me to explain?");
    }

    [Fact]
    public void Rasa_AntiPatterns_ForbidsFollowUpQuestions()
    {
        Agents.General.AntiPatternsBlock.Should().Contain("NEVER end your response with a follow-up question");
        Agents.General.AntiPatternsBlock.Should().Contain("want me to explain?");
    }

    [Fact]
    public void Leroy_Behavior_DoesNotEncourageQuestions()
    {
        // "Often ask" pattern was removed
        Agents.Chess.BehaviorBlock.Should().NotContain("Often ask");
        Agents.Chess.BehaviorBlock.Should().NotContain("quick move or the why");
    }

    [Fact]
    public void Rasa_Behavior_DoesNotEncourageFavoriteQuestion()
    {
        // "Your favorite question" and "Ask one good question" were removed
        Agents.General.BehaviorBlock.Should().NotContain("Your favorite question");
        Agents.General.BehaviorBlock.Should().NotContain("Ask one good question");
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
    public void BuildSystemPrompt_WithRasa_ContainsRasaIdentity()
    {
        var sut = CreateSut();

        var result = sut.BuildSystemPrompt(OutGameSession(), SampleTools(), Agents.General);

        // RASA has full SoulBlock — should use RASA identity, not Dross
        result.Should().Contain("RASA");
        result.Should().NotContain("Dross");
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
