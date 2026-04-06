using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.GamePacks;

public class PackDrivenPromptBuilderTests
{
    private readonly BrainPromptBuilder _sut;
    private readonly GameSkillPack _chessPack;

    public PackDrivenPromptBuilderTests()
    {
        _chessPack = new GameSkillPack
        {
            Id = "chess-online",
            Genre = "chess",
            BrainInstructionsContent = "[CAPABILITIES]\nYou see chess boards.\n\n[OUTPUT FORMAT]\nProvide analysis.",
            UserPromptTemplate = "Current game: chess. Position #{moveNumber}."
        };

        var mockPackService = new TestPackService(_chessPack);
        _sut = new BrainPromptBuilder(mockPackService);
    }

    [Fact]
    public void BuildSystemPrompt_WithPack_InjectsBrainInstructions()
    {
        var agent = CreateTestAgent();
        var prompt = _sut.BuildSystemPrompt(agent, Array.Empty<BrainEvent>(),
            null, null, null, true);

        Assert.Contains("[CAPABILITIES]", prompt);
        Assert.Contains("You see chess boards.", prompt);
        Assert.Contains("[OUTPUT FORMAT]", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithPack_IncludesAgentIdentity()
    {
        var agent = CreateTestAgent();
        var prompt = _sut.BuildSystemPrompt(agent, Array.Empty<BrainEvent>(),
            null, null, null, true);

        if (!string.IsNullOrWhiteSpace(agent.BrainPersonalityPrefix))
            Assert.Contains(agent.BrainPersonalityPrefix.Trim(), prompt);
    }

    [Fact]
    public void BuildSystemPrompt_NoPack_ProducesGenericPrompt()
    {
        var mockPackService = new TestPackService(null);
        var sut = new BrainPromptBuilder(mockPackService);
        var agent = CreateTestAgent();

        var prompt = sut.BuildSystemPrompt(agent, Array.Empty<BrainEvent>(),
            null, null, null, true);

        Assert.DoesNotContain("Stockfish", prompt);
        Assert.Contains("Describe what you see", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithPackTemplate_UsesTemplate()
    {
        var prompt = _sut.BuildUserPrompt("chess", 5);
        Assert.Equal("Current game: chess. Position #5.", prompt);
    }

    [Fact]
    public void BuildUserPrompt_NoPack_ReturnsFallback()
    {
        var mockPackService = new TestPackService(null);
        var sut = new BrainPromptBuilder(mockPackService);

        var prompt = sut.BuildUserPrompt("unknown", 0);
        Assert.Equal("Analyze this frame.", prompt);
    }

    private static Agent CreateTestAgent() => new()
    {
        Key = "test", Id = "test", Name = "Test",
        PrimaryGame = "Chess", IconImage = "", PortraitImage = "",
        Description = "", Features = new(), SystemInstruction = "",
        Type = AgentType.Chess,
        BrainPersonalityPrefix = "[IDENTITY] Test agent"
    };

    private class TestPackService : IGameSkillPackService
    {
        public GameSkillPack? ActivePack { get; }
        public TestPackService(GameSkillPack? pack) => ActivePack = pack;
        public GameSkillPack? LoadPack(string packId) => ActivePack;
        public void SetActivePack(string? packId) { }
        public IReadOnlyList<string> GetAvailablePackIds() => Array.Empty<string>();
    }
}
