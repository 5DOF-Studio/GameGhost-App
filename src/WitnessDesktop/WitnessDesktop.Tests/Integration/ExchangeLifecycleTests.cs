using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Audio;

namespace WitnessDesktop.Tests.Integration;

public class ExchangeLifecycleTests
{
    // ── Full Lifecycle: Dormant → Wake → Active → Silence → Dormant ──

    [Fact]
    public void FullLifecycle_DormantToWakeToActiveToSilenceToDormant()
    {
        var exchange = new ExchangeManager(silenceTimeout: TimeSpan.FromMilliseconds(80));
        exchange.SetMode(AudioIntelligenceMode.Full);

        var states = new List<ExchangeState>();
        exchange.ExchangeStateChanged += (_, s) => states.Add(s);

        // Dormant → Wake → Active
        exchange.OnWakeDetected("Leroy");
        exchange.CurrentState.Should().Be(ExchangeState.ExchangeActive);

        // Active → speech resets timer
        Thread.Sleep(50);
        exchange.OnUserSpeech();

        // Silence → Dormant
        Thread.Sleep(120);
        exchange.CurrentState.Should().Be(ExchangeState.Dormant);

        states.Should().Contain(ExchangeState.WakeDetected);
        states.Should().Contain(ExchangeState.ExchangeActive);
        states.Should().Contain(ExchangeState.Dormant);
    }

    // ── Exchange + VoiceDeliveryGate: Active delivers, Dormant queues ──

    [Fact]
    public void Gate_ActiveExchange_Delivers_DormantExchange_Queues()
    {
        var exchange = new ExchangeManager();
        exchange.SetMode(AudioIntelligenceMode.Full);
        var bargeIn = new BargeInPolicyService();
        bargeIn.SetEnabled(false);
        var gate = new VoiceDeliveryGate(exchange, bargeIn);

        // Dormant + barge-in disabled → QueueReminder
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.QueueReminder);

        // Open exchange → Deliver
        exchange.OnWakeDetected("Leroy");
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.Deliver);

        // Close → QueueReminder again
        exchange.CloseExchange();
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.QueueReminder);
    }

    // ── Barge-In: Enabled + User Silent → Deliver ──

    [Fact]
    public void BargeIn_Enabled_UserSilent_Delivers()
    {
        var exchange = new ExchangeManager();
        exchange.SetMode(AudioIntelligenceMode.Full);
        var bargeIn = new BargeInPolicyService();
        bargeIn.SetEnabled(true);
        var userSpeech = new UserSpeechDetector();
        var gate = new VoiceDeliveryGate(exchange, bargeIn, userSpeech);

        // Exchange dormant + barge-in enabled + user silent → Deliver
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.Deliver);
    }

    // ── Barge-In: User Speaking → QueueReminder (D-AI-4) ──

    [Fact]
    public void BargeIn_UserSpeaking_QueuesReminder()
    {
        var exchange = new ExchangeManager();
        exchange.SetMode(AudioIntelligenceMode.Full);
        var bargeIn = new BargeInPolicyService();
        bargeIn.SetEnabled(true);
        var userSpeech = new UserSpeechDetector(speechThreshold: 0.01f, debounceMs: 1000);
        userSpeech.OnLevelChanged(0.5f); // User is speaking
        var gate = new VoiceDeliveryGate(exchange, bargeIn, userSpeech);

        // D-AI-4: NEVER barge in while user speaking
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert)
            .Should().Be(DeliveryDecision.QueueReminder);
    }

    // ── Reminder Lifecycle: Queue → Exchange Open → Surface ──

    [Fact]
    public void ReminderLifecycle_QueueThenSurfaceOnExchangeOpen()
    {
        var queue = new ReminderQueue();
        var exchange = new ExchangeManager();
        exchange.SetMode(AudioIntelligenceMode.Full);

        // Queue a reminder
        queue.Enqueue(new ReminderItem
        {
            Content = "You had a blunder on move 15",
            Category = BargeInCategory.CallOut,
        });
        queue.Count.Should().Be(1);

        // Exchange opens → dequeue
        ExchangeSession? opened = null;
        exchange.ExchangeOpened += (_, session) =>
        {
            opened = session;
            // Surface one reminder (simulating MainViewModel behavior)
            var reminder = queue.Dequeue();
            reminder.Should().NotBeNull();
            reminder!.Content.Should().Contain("blunder");
        };

        exchange.OnWakeDetected("Leroy");
        opened.Should().NotBeNull();
        queue.Count.Should().Be(0);
    }

    // ── Wake Phrase Detection → Exchange Open ──

    [Fact]
    public void WakePhrase_DetectedInTranscript_OpensExchange()
    {
        var detector = new TranscriptWakePhraseDetector();
        var exchange = new ExchangeManager();
        exchange.SetMode(AudioIntelligenceMode.Full);
        var transcript = "hey Leroy what's going on?";

        if (detector.TryDetectWake(transcript, "Leroy", out _) && !exchange.IsExchangeActive)
        {
            exchange.OnWakeDetected("Leroy");
        }

        exchange.IsExchangeActive.Should().BeTrue();
        exchange.CurrentExchange!.AgentName.Should().Be("Leroy");
    }

    // ── Turn Classification → DeferToBrain ──

    [Fact]
    public void VoiceDeferral_HistorySensitive_ReturnsDeferToBrain()
    {
        var coordinator = new VoiceGroundingCoordinator();
        var decision = coordinator.Evaluate("What happened earlier?", isInGame: true);

        decision.ResponseMode.Should().Be(VoiceResponseMode.DeferToBrain);
        decision.TurnClass.Should().Be(VoiceTurnClass.HistorySensitive);
    }

    // ── Degradation: TextOnly → No Exchange ──

    [Fact]
    public void TextOnly_WakeDetected_NoExchange()
    {
        var exchange = new ExchangeManager();
        exchange.SetMode(AudioIntelligenceMode.TextOnly);

        exchange.OnWakeDetected("Leroy");
        exchange.IsExchangeActive.Should().BeFalse();
    }
}
