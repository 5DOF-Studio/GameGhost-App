using FluentAssertions;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;

namespace WitnessDesktop.Tests.Services;

public class ExchangeManagerTests
{
    private readonly ExchangeManager _sut = new();

    [Fact]
    public void InitialState_IsDormant()
    {
        _sut.CurrentState.Should().Be(ExchangeState.Dormant);
        _sut.CurrentExchange.Should().BeNull();
        _sut.IsExchangeActive.Should().BeFalse();
    }

    [Fact]
    public void OnWakeDetected_FromDormant_TransitionsToExchangeActive()
    {
        _sut.OnWakeDetected("Leroy");
        _sut.CurrentState.Should().Be(ExchangeState.ExchangeActive);
        _sut.IsExchangeActive.Should().BeTrue();
        _sut.CurrentExchange.Should().NotBeNull();
        _sut.CurrentExchange!.AgentName.Should().Be("Leroy");
        _sut.CurrentExchange.ExchangeId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void OnWakeDetected_FiresExchangeOpenedEvent()
    {
        ExchangeSession? opened = null;
        _sut.ExchangeOpened += (_, session) => opened = session;
        _sut.OnWakeDetected("Wasp");
        opened.Should().NotBeNull();
        opened!.AgentName.Should().Be("Wasp");
        opened.State.Should().Be(ExchangeState.ExchangeActive);
    }

    [Fact]
    public void OnWakeDetected_FiresStateChangedEvents()
    {
        var states = new List<ExchangeState>();
        _sut.ExchangeStateChanged += (_, state) => states.Add(state);
        _sut.OnWakeDetected("Leroy");
        states.Should().ContainInOrder(
            ExchangeState.WakeDetected,
            ExchangeState.ExchangeOpening,
            ExchangeState.ExchangeActive);
    }

    [Fact]
    public void OnWakeDetected_StateCallbacksObserveMatchingCurrentState()
    {
        var observedStates = new List<(ExchangeState EventState, ExchangeState CurrentState)>();
        _sut.ExchangeStateChanged += (_, state) => observedStates.Add((state, _sut.CurrentState));

        _sut.OnWakeDetected("Leroy");

        observedStates.Should().ContainInOrder(
            (ExchangeState.WakeDetected, ExchangeState.WakeDetected),
            (ExchangeState.ExchangeOpening, ExchangeState.ExchangeOpening),
            (ExchangeState.ExchangeActive, ExchangeState.ExchangeActive));
    }

    [Fact]
    public void OnWakeDetected_WhenAlreadyActive_IsIgnored()
    {
        _sut.OnWakeDetected("Leroy");
        var exchangeId = _sut.CurrentExchange!.ExchangeId;
        _sut.OnWakeDetected("Leroy");
        _sut.CurrentExchange!.ExchangeId.Should().Be(exchangeId);
    }

    [Fact]
    public void OnUserSpeech_WhenActive_UpdatesLastActivity()
    {
        _sut.OnWakeDetected("Leroy");
        var before = _sut.CurrentExchange!.LastActivityUtc;
        Thread.Sleep(10);
        _sut.OnUserSpeech();
        _sut.CurrentExchange!.LastActivityUtc.Should().BeAfter(before);
    }

    [Fact]
    public void OnAgentSpeech_WhenActive_UpdatesLastActivity()
    {
        _sut.OnWakeDetected("Leroy");
        var before = _sut.CurrentExchange!.LastActivityUtc;
        Thread.Sleep(10);
        _sut.OnAgentSpeech();
        _sut.CurrentExchange!.LastActivityUtc.Should().BeAfter(before);
    }

    [Fact]
    public void OnUserSpeech_WhenDormant_IsIgnored()
    {
        _sut.OnUserSpeech();
        _sut.CurrentState.Should().Be(ExchangeState.Dormant);
    }

    [Fact]
    public void CloseExchange_TransitionsToDormant()
    {
        _sut.OnWakeDetected("Leroy");
        _sut.CloseExchange();
        _sut.CurrentState.Should().Be(ExchangeState.Dormant);
        _sut.IsExchangeActive.Should().BeFalse();
        _sut.CurrentExchange.Should().BeNull();
    }

    [Fact]
    public void CloseExchange_FiresExchangeClosedEvent()
    {
        ExchangeSession? closed = null;
        _sut.ExchangeClosed += (_, session) => closed = session;
        _sut.OnWakeDetected("Leroy");
        _sut.CloseExchange();
        closed.Should().NotBeNull();
        closed!.AgentName.Should().Be("Leroy");
    }

    [Fact]
    public void CloseExchange_WhenDormant_IsIgnored()
    {
        _sut.CloseExchange();
        _sut.CurrentState.Should().Be(ExchangeState.Dormant);
    }

    [Fact]
    public void TransitionToAwaitingBrain_FromActive_Transitions()
    {
        _sut.OnWakeDetected("Leroy");
        _sut.TransitionToAwaitingBrain();
        _sut.CurrentState.Should().Be(ExchangeState.AwaitingBrain);
        _sut.IsExchangeActive.Should().BeTrue();
    }

    [Fact]
    public void TransitionToAwaitingBrain_FromDormant_IsIgnored()
    {
        _sut.TransitionToAwaitingBrain();
        _sut.CurrentState.Should().Be(ExchangeState.Dormant);
    }

    [Fact]
    public void SilenceTimer_WhenFired_ClosesExchange()
    {
        var sut = new ExchangeManager(silenceTimeout: TimeSpan.FromMilliseconds(50));
        ExchangeSession? closed = null;
        sut.ExchangeClosed += (_, session) => closed = session;
        sut.OnWakeDetected("Leroy");
        Thread.Sleep(150);
        sut.CurrentState.Should().Be(ExchangeState.Dormant);
        closed.Should().NotBeNull();
    }

    [Fact]
    public void SilenceTimer_ResetByUserSpeech_DoesNotFireEarly()
    {
        var sut = new ExchangeManager(silenceTimeout: TimeSpan.FromMilliseconds(100));
        sut.OnWakeDetected("Leroy");
        Thread.Sleep(60);
        sut.OnUserSpeech();
        Thread.Sleep(60);
        sut.CurrentState.Should().Be(ExchangeState.ExchangeActive);
    }

    [Fact]
    public void OnWakeDetected_AfterClose_StartsNewExchange()
    {
        _sut.OnWakeDetected("Leroy");
        var firstId = _sut.CurrentExchange!.ExchangeId;
        _sut.CloseExchange();
        _sut.OnWakeDetected("Wasp");
        _sut.CurrentState.Should().Be(ExchangeState.ExchangeActive);
        _sut.CurrentExchange!.ExchangeId.Should().NotBe(firstId);
        _sut.CurrentExchange.AgentName.Should().Be("Wasp");
    }

    [Fact]
    public void SilenceTimer_ResetByAgentSpeech_DoesNotFireEarly()
    {
        var sut = new ExchangeManager(silenceTimeout: TimeSpan.FromMilliseconds(100));
        sut.OnWakeDetected("Leroy");
        Thread.Sleep(60);
        sut.OnAgentSpeech();
        Thread.Sleep(60);
        sut.CurrentState.Should().Be(ExchangeState.ExchangeActive);
    }

    [Fact]
    public void Dispose_StopsTimer_NoExceptions()
    {
        var sut = new ExchangeManager(silenceTimeout: TimeSpan.FromMilliseconds(50));
        sut.OnWakeDetected("Leroy");
        sut.Dispose();
        Thread.Sleep(100);
    }

    [Fact]
    public void TransitionToAwaitingBrain_ResetsSilenceTimer()
    {
        var sut = new ExchangeManager(silenceTimeout: TimeSpan.FromMilliseconds(100));
        sut.OnWakeDetected("Leroy");
        Thread.Sleep(70); // 70ms into 100ms timeout
        sut.TransitionToAwaitingBrain(); // Resets timer — 100ms from now
        Thread.Sleep(70); // 70ms after reset — still within window
        sut.CurrentState.Should().Be(ExchangeState.AwaitingBrain);
        sut.IsExchangeActive.Should().BeTrue();
    }
}
