using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Services;

public class VoiceDeliveryGateBargeInTests
{
    // Path 4: inactive + barge-in disabled → QueueReminder
    [Fact]
    public void Inactive_BargeInDisabled_ReturnsQueueReminder()
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: false);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.QueueReminder);
    }

    // Path 5: inactive + barge-in enabled + category not allowed → QueueReminder
    [Fact]
    public void Inactive_BargeInEnabled_CategoryDisabled_ReturnsQueueReminder()
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: true,
            disabledCategories: new[] { BargeInCategory.CallOut });
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.QueueReminder);
    }

    // Path 6: inactive + barge-in enabled + allowed + user speaking → QueueReminder (D-AI-4)
    [Fact]
    public void Inactive_BargeInEnabled_UserSpeaking_ReturnsQueueReminder()
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: true, userSpeaking: true);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.QueueReminder);
    }

    // Path 7: inactive + barge-in enabled + allowed + user silent → Deliver
    [Fact]
    public void Inactive_BargeInEnabled_UserSilent_ReturnsDeliver()
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: true, userSpeaking: false);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.Deliver);
    }

    // Category mapping tests — all three valid types can barge-in when allowed
    [Theory]
    [InlineData(BrainResultType.ProactiveAlert)]
    [InlineData(BrainResultType.ToolResult)]
    [InlineData(BrainResultType.ImageAnalysis)]
    public void Inactive_BargeInEnabled_AllTypes_CanDeliver(BrainResultType type)
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: true, userSpeaking: false);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, type)
            .Should().Be(DeliveryDecision.Deliver);
    }

    // Error type → Suppress (no barge-in category)
    [Fact]
    public void Inactive_BargeInEnabled_ErrorType_ReturnsSuppressed()
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: true, userSpeaking: false);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.Error)
            .Should().Be(DeliveryDecision.Suppress);
    }

    // Backward compat: no barge-in service → Suppress (pre-12C)
    [Fact]
    public void Inactive_NoBargeInService_ReturnsSuppressed()
    {
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.Dormant));
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.Suppress);
    }

    // Existing behavior preserved: exchange active still delivers even with barge-in disabled
    [Fact]
    public void Active_WithBargeIn_StillDelivers()
    {
        var sut = CreateGate(ExchangeState.ExchangeActive, bargeInEnabled: false);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.Deliver);
    }

    // Old single-param overload still works
    [Fact]
    public void SingleParamOverload_BackwardCompatible()
    {
        var sut = CreateGate(ExchangeState.ExchangeActive, bargeInEnabled: false);
        sut.ShouldDeliver(BrainResultPriority.WhenIdle)
            .Should().Be(DeliveryDecision.Deliver);
    }

    // Silent always suppresses — even with barge-in enabled
    [Fact]
    public void Silent_WithBargeInEnabled_StillSuppresses()
    {
        var sut = CreateGate(ExchangeState.Dormant, bargeInEnabled: true, userSpeaking: false);
        sut.ShouldDeliver(BrainResultPriority.Silent, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.Suppress);
    }

    // -- Helpers --

    private static VoiceDeliveryGate CreateGate(
        ExchangeState state,
        bool bargeInEnabled = false,
        bool userSpeaking = false,
        BargeInCategory[]? disabledCategories = null)
    {
        var policy = new StubBargeInPolicy(bargeInEnabled, disabledCategories);
        var detector = new StubUserSpeechDetector(userSpeaking);
        return new VoiceDeliveryGate(
            new StubExchangeManager(state),
            policy,
            detector);
    }

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

    private sealed class StubBargeInPolicy : IBargeInPolicyService
    {
        private readonly bool _enabled;
        private readonly HashSet<BargeInCategory> _disabled;
        public StubBargeInPolicy(bool enabled, BargeInCategory[]? disabled = null)
        {
            _enabled = enabled;
            _disabled = new HashSet<BargeInCategory>(disabled ?? Array.Empty<BargeInCategory>());
        }
        public BargeInPolicy CurrentPolicy => new() { IsEnabled = _enabled };
        public bool IsBargeInEnabled => _enabled;
        public void SetEnabled(bool enabled) { }
        public void SetCategoryEnabled(BargeInCategory category, bool enabled) { }
        public bool IsCategoryAllowed(BargeInCategory category) => _enabled && !_disabled.Contains(category);
        public event EventHandler<BargeInPolicy>? PolicyChanged;
    }

    private sealed class StubUserSpeechDetector : IUserSpeechDetector
    {
        private readonly bool _speaking;
        public StubUserSpeechDetector(bool speaking) => _speaking = speaking;
        public bool IsUserSpeaking => _speaking;
        public float CurrentLevel => 0f;
        public void OnLevelChanged(float level) { }
        public event EventHandler? UserSpeechStarted;
        public event EventHandler? UserSpeechStopped;
        public void Dispose() { }
    }
}
