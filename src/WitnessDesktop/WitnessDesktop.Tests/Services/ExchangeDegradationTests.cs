using FluentAssertions;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class ExchangeDegradationTests
{
    [Fact]
    public void DefaultMode_IsFull()
    {
        var sut = new ExchangeManager();
        sut.CurrentMode.Should().Be(AudioIntelligenceMode.Full);
    }

    [Fact]
    public void SetMode_Persists()
    {
        var sut = new ExchangeManager();
        sut.SetMode(AudioIntelligenceMode.VoiceOnly);
        sut.CurrentMode.Should().Be(AudioIntelligenceMode.VoiceOnly);
    }

    [Fact]
    public void TextOnly_OnWakeDetected_IsNoOp()
    {
        var sut = new ExchangeManager();
        sut.SetMode(AudioIntelligenceMode.TextOnly);
        sut.OnWakeDetected("Leroy");
        sut.CurrentState.Should().Be(ExchangeState.Dormant); // No exchange opened
    }

    [Fact]
    public void VoiceOnly_OnWakeDetected_OpensExchange()
    {
        var sut = new ExchangeManager();
        sut.SetMode(AudioIntelligenceMode.VoiceOnly);
        sut.OnWakeDetected("Leroy");
        sut.CurrentState.Should().Be(ExchangeState.ExchangeActive);
    }

    [Fact]
    public void Full_OnWakeDetected_OpensExchange()
    {
        var sut = new ExchangeManager();
        sut.SetMode(AudioIntelligenceMode.Full);
        sut.OnWakeDetected("Leroy");
        sut.CurrentState.Should().Be(ExchangeState.ExchangeActive);
    }
}
