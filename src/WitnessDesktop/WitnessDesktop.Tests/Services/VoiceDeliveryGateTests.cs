using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class VoiceDeliveryGateTests
{
    [Theory]
    [InlineData(BrainResultPriority.Interrupt)]
    [InlineData(BrainResultPriority.WhenIdle)]
    public void ShouldDeliver_ExchangeActive_ReturnsDeliver(BrainResultPriority priority)
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.ExchangeActive));
        sut.ShouldDeliver(priority).Should().Be(DeliveryDecision.Deliver);
    }

    [Fact]
    public void ShouldDeliver_AwaitingBrain_ReturnsDeliver()
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.AwaitingBrain));
        sut.ShouldDeliver(BrainResultPriority.WhenIdle).Should().Be(DeliveryDecision.Deliver);
    }

    [Fact]
    public void ShouldDeliver_ExchangeOpening_ReturnsDeliver()
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.ExchangeOpening));
        sut.ShouldDeliver(BrainResultPriority.WhenIdle).Should().Be(DeliveryDecision.Deliver);
    }

    [Fact]
    public void ShouldDeliver_Dormant_InterruptPriority_ReturnsDeliver()
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.Dormant));
        sut.ShouldDeliver(BrainResultPriority.Interrupt).Should().Be(DeliveryDecision.Deliver);
    }

    [Theory]
    [InlineData(BrainResultPriority.WhenIdle)]
    [InlineData(BrainResultPriority.Silent)]
    public void ShouldDeliver_Dormant_NonInterrupt_ReturnsSuppressed(BrainResultPriority priority)
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.Dormant));
        sut.ShouldDeliver(priority).Should().Be(DeliveryDecision.Suppress);
    }

    [Theory]
    [InlineData(ExchangeState.ExchangeExpired)]
    [InlineData(ExchangeState.ExchangeClosing)]
    [InlineData(ExchangeState.ReminderQueued)]
    public void ShouldDeliver_InactiveStates_WhenIdle_ReturnsSuppressed(ExchangeState state)
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(state));
        sut.ShouldDeliver(BrainResultPriority.WhenIdle).Should().Be(DeliveryDecision.Suppress);
    }

    [Fact]
    public void ShouldDeliver_SilentPriority_ActiveExchange_ReturnsSuppressed()
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.ExchangeActive));
        sut.ShouldDeliver(BrainResultPriority.Silent).Should().Be(DeliveryDecision.Suppress);
    }

    [Fact]
    public void ShouldDeliver_SilentPriority_DormantExchange_ReturnsSuppressed()
    {
        var sut = new VoiceDeliveryGate(new StubExchangeManager(ExchangeState.Dormant));
        sut.ShouldDeliver(BrainResultPriority.Silent).Should().Be(DeliveryDecision.Suppress);
    }

    /// <summary>Minimal stub — returns fixed state for testing gate logic.</summary>
    private sealed class StubExchangeManager : IExchangeManager
    {
        private readonly ExchangeState _state;
        public StubExchangeManager(ExchangeState state) => _state = state;
        public ExchangeState CurrentState => _state;
        public ExchangeSession? CurrentExchange => null;
        public bool IsExchangeActive => _state is ExchangeState.WakeDetected
            or ExchangeState.ExchangeOpening or ExchangeState.ExchangeActive or ExchangeState.AwaitingBrain;
        public AudioIntelligenceMode CurrentMode => AudioIntelligenceMode.Full;
        public void SetMode(AudioIntelligenceMode mode) { }
        public void OnWakeDetected(string agentName) { }
        public void OnUserSpeech() { }
        public void OnAgentSpeech() { }
        public void CloseExchange() { }
        public void TransitionToAwaitingBrain() { }
        public event EventHandler<ExchangeState>? ExchangeStateChanged;
        public event EventHandler<ExchangeSession>? ExchangeOpened;
        public event EventHandler<ExchangeSession>? ExchangeClosed;
    }
}
