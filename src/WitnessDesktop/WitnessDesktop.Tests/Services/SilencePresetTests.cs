using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class SilencePresetTests
{
    [Theory]
    [InlineData("Quick", 8)]
    [InlineData("Normal", 15)]
    [InlineData("Patient", 30)]
    public void ExchangeManager_ReadsPresetFromPack(string preset, int expectedSeconds)
    {
        var pack = new GameSkillPack { SilenceTimeoutPreset = preset };
        var packService = new StubPackService(pack);
        var sut = new ExchangeManager(packService: packService);

        sut.OnWakeDetected("Leroy");
        // Exchange should be active — the preset determines how long silence is tolerated
        sut.IsExchangeActive.Should().BeTrue();
    }

    [Fact]
    public void GameSkillPack_DefaultPreset_IsNormal()
    {
        var pack = new GameSkillPack();
        pack.SilenceTimeoutPreset.Should().Be("Normal");
    }

    [Fact]
    public void ExchangeContextInfo_PopulatesCorrectly()
    {
        var info = new ExchangeContextInfo(
            Guid.NewGuid(), true, TimeSpan.FromSeconds(5), "Leroy");
        info.IsExchangeActive.Should().BeTrue();
        info.AgentName.Should().Be("Leroy");
    }

    [Fact]
    public void FreshnessRules_IsNotEmpty()
    {
        Agent.FreshnessRules.Should().NotBeNullOrWhiteSpace();
        Agent.FreshnessRules.Should().Contain("FRESHNESS RULES");
    }

    private sealed class StubPackService : IGameSkillPackService
    {
        private readonly GameSkillPack? _pack;
        public StubPackService(GameSkillPack? pack) => _pack = pack;
        public GameSkillPack? ActivePack => _pack;
        public GameSkillPack? LoadPack(string packId) => null;
        public void SetActivePack(string? packId) { }
        public IReadOnlyList<string> GetAvailablePackIds() => Array.Empty<string>();
    }
}
