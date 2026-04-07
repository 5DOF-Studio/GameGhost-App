using FluentAssertions;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Exchange;
using WitnessDesktop.Services;
using WitnessDesktop.Services.Audio;
using WitnessDesktop.Services.Replay;

namespace WitnessDesktop.Tests.Services;

/// <summary>
/// Telemetry coverage tests for all services instrumented since March 2026.
/// Each test verifies that the correct trace event is emitted with the expected
/// event name and payload keys. Uses SpySessionTraceService for capture.
/// </summary>
public class TelemetryCoverageTests
{
    // ═══════════════════════════════════════════════════════════════════
    //  Wave 1: Audio Intelligence Pipeline
    // ═══════════════════════════════════════════════════════════════════

    // ── VoiceDeliveryGate ─────────────────────────────────────────────

    [Fact]
    public void VoiceDeliveryGate_EmitsDeliveryDecision_OnDeliver()
    {
        var spy = new SpySessionTraceService();
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.ExchangeActive),
            sessionTrace: spy);

        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ImageAnalysis);

        spy.Events.Should().ContainSingle(e => e.EventName == "voice.delivery.decision");
        var evt = spy.Events.First(e => e.EventName == "voice.delivery.decision");
        evt.Payload.Should().ContainKey("result_type");
        evt.Payload.Should().ContainKey("decision");
        evt.Payload.Should().ContainKey("reason");
        evt.Payload!["decision"].Should().Be("Deliver");
        evt.Payload["reason"].Should().Be("exchange_active");
    }

    [Fact]
    public void VoiceDeliveryGate_EmitsDeliveryDecision_OnSuppress()
    {
        var spy = new SpySessionTraceService();
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.Dormant),
            sessionTrace: spy);

        gate.ShouldDeliver(BrainResultPriority.Silent, BrainResultType.ImageAnalysis);

        var evt = spy.Events.First(e => e.EventName == "voice.delivery.decision");
        evt.Payload!["decision"].Should().Be("Suppress");
        evt.Payload["reason"].Should().Be("silent_priority");
    }

    [Fact]
    public void VoiceDeliveryGate_EmitsDeliveryDecision_OnQueueReminder()
    {
        var spy = new SpySessionTraceService();
        var policy = new StubBargeInPolicy(enabled: true);
        var userDetector = new StubUserSpeechDetector(speaking: true);
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.Dormant),
            bargeInPolicy: policy,
            userSpeechDetector: userDetector,
            sessionTrace: spy);

        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ProactiveAlert);

        var evt = spy.Events.First(e => e.EventName == "voice.delivery.decision");
        evt.Payload!["decision"].Should().Be("QueueReminder");
        evt.Payload["reason"].Should().Be("user_speaking");
    }

    [Fact]
    public void VoiceDeliveryGate_SingleParamOverload_EmitsDecision()
    {
        var spy = new SpySessionTraceService();
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.ExchangeActive),
            sessionTrace: spy);

        gate.ShouldDeliver(BrainResultPriority.WhenIdle);

        spy.Events.Should().ContainSingle(e => e.EventName == "voice.delivery.decision");
    }

    // ── ReminderQueue ─────────────────────────────────────────────────

    [Fact]
    public void ReminderQueue_EmitsEnqueued_OnEnqueue()
    {
        var spy = new SpySessionTraceService();
        var queue = new ReminderQueue(sessionTrace: spy);

        queue.Enqueue(MakeReminder("test", BargeInCategory.CallOut));

        spy.Events.Should().ContainSingle(e => e.EventName == "voice.reminder.enqueued");
        var evt = spy.Events.First(e => e.EventName == "voice.reminder.enqueued");
        evt.Payload!["category"].Should().Be("CallOut");
        evt.Payload["queue_depth"].Should().Be("1");
    }

    [Fact]
    public void ReminderQueue_EmitsDequeued_OnDequeue()
    {
        var spy = new SpySessionTraceService();
        var queue = new ReminderQueue(sessionTrace: spy);
        queue.Enqueue(MakeReminder("test", BargeInCategory.Reminder));

        queue.Dequeue();

        spy.Events.Should().Contain(e => e.EventName == "voice.reminder.dequeued");
        var evt = spy.Events.First(e => e.EventName == "voice.reminder.dequeued");
        evt.Payload!["category"].Should().Be("Reminder");
        evt.Payload.Should().ContainKey("age_ms");
    }

    [Fact]
    public void ReminderQueue_EmitsSuperseded_OnSupersede()
    {
        var spy = new SpySessionTraceService();
        var queue = new ReminderQueue(sessionTrace: spy);
        queue.Enqueue(MakeReminder("old", BargeInCategory.CallOut));

        queue.Supersede(BargeInCategory.CallOut, MakeReminder("new", BargeInCategory.CallOut));

        spy.Events.Should().Contain(e => e.EventName == "voice.reminder.superseded");
        var evt = spy.Events.First(e => e.EventName == "voice.reminder.superseded");
        evt.Payload!["category"].Should().Be("CallOut");
    }

    [Fact]
    public void ReminderQueue_EmitsPruned_OnPruneStale()
    {
        var spy = new SpySessionTraceService();
        // Use a very large maxAge so items survive Enqueue's internal pruning
        var queue = new ReminderQueue(maxAge: TimeSpan.FromHours(1), sessionTrace: spy);
        // Add a fresh item, then prune with very short maxAge
        queue.Enqueue(MakeReminder("soon-stale", BargeInCategory.Reminder,
            DateTime.UtcNow.AddSeconds(-2)));

        // Prune with 1ms max age — the 2-second-old item should be pruned
        queue.PruneStale(TimeSpan.FromMilliseconds(1));

        spy.Events.Should().Contain(e => e.EventName == "voice.reminder.pruned");
        var evt = spy.Events.First(e => e.EventName == "voice.reminder.pruned");
        evt.Payload.Should().ContainKey("count");
        evt.Payload.Should().ContainKey("oldest_age_ms");
    }

    [Fact]
    public void ReminderQueue_Dequeue_DoesNotEmit_WhenEmpty()
    {
        var spy = new SpySessionTraceService();
        var queue = new ReminderQueue(sessionTrace: spy);

        queue.Dequeue();

        spy.Events.Should().NotContain(e => e.EventName == "voice.reminder.dequeued");
    }

    // ── UserSpeechDetector ────────────────────────────────────────────

    [Fact]
    public void UserSpeechDetector_EmitsStarted_WhenSpeechBegins()
    {
        var spy = new SpySessionTraceService();
        var detector = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 300, sessionTrace: spy);

        detector.OnLevelChanged(0.05f); // Above threshold

        spy.Events.Should().ContainSingle(e => e.EventName == "audio.user_speech.started");
    }

    [Fact]
    public void UserSpeechDetector_EmitsStopped_WhenSpeechEnds()
    {
        var spy = new SpySessionTraceService();
        var detector = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 50, sessionTrace: spy);

        detector.OnLevelChanged(0.05f); // Start speaking
        detector.OnLevelChanged(0.001f); // Drop below threshold

        // Wait for debounce
        Thread.Sleep(100);

        spy.Events.Should().Contain(e => e.EventName == "audio.user_speech.stopped");
        var evt = spy.Events.First(e => e.EventName == "audio.user_speech.stopped");
        evt.Payload.Should().ContainKey("duration_ms");
    }

    [Fact]
    public void UserSpeechDetector_DoesNotDuplicateStarted()
    {
        var spy = new SpySessionTraceService();
        var detector = new UserSpeechDetector(speechThreshold: 0.02f, debounceMs: 300, sessionTrace: spy);

        detector.OnLevelChanged(0.05f);
        detector.OnLevelChanged(0.06f); // Still above — should not re-emit

        spy.Events.Where(e => e.EventName == "audio.user_speech.started").Should().HaveCount(1);
    }

    // ── AgentSpeechTracker ────────────────────────────────────────────

    [Fact]
    public void AgentSpeechTracker_EmitsStarted_OnFirstAudio()
    {
        var spy = new SpySessionTraceService();
        var tracker = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(500), sessionTrace: spy);

        tracker.OnAudioReceived();

        spy.Events.Should().ContainSingle(e => e.EventName == "audio.agent_speech.started");
    }

    [Fact]
    public void AgentSpeechTracker_EmitsStopped_AfterSilenceGap()
    {
        var spy = new SpySessionTraceService();
        var tracker = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(50), sessionTrace: spy);

        tracker.OnAudioReceived();
        Thread.Sleep(100);

        spy.Events.Should().Contain(e => e.EventName == "audio.agent_speech.stopped");
        var evt = spy.Events.First(e => e.EventName == "audio.agent_speech.stopped");
        evt.Payload.Should().ContainKey("duration_ms");
    }

    [Fact]
    public void AgentSpeechTracker_DoesNotDuplicateStarted()
    {
        var spy = new SpySessionTraceService();
        var tracker = new AgentSpeechTracker(silenceGap: TimeSpan.FromMilliseconds(500), sessionTrace: spy);

        tracker.OnAudioReceived();
        tracker.OnAudioReceived(); // Still active — should not re-emit

        spy.Events.Where(e => e.EventName == "audio.agent_speech.started").Should().HaveCount(1);
    }

    // ── TranscriptWakePhraseDetector ──────────────────────────────────

    [Fact]
    public void WakeDetector_EmitsFuzzyMatch_OnExactMatch()
    {
        var spy = new SpySessionTraceService();
        var detector = new TranscriptWakePhraseDetector(sessionTrace: spy);

        detector.TryDetectWake("hey Leroy how are you", "Leroy", out _);

        spy.Events.Should().ContainSingle(e => e.EventName == "audio.wake.fuzzy_match");
        var evt = spy.Events.First(e => e.EventName == "audio.wake.fuzzy_match");
        evt.Payload!["matched"].Should().Be("True");
        evt.Payload["confidence"].Should().Be("1.00");
        evt.Payload["phrase"].Should().Be("hey Leroy");
    }

    [Fact]
    public void WakeDetector_EmitsFuzzyMatch_OnFuzzyMatch()
    {
        var spy = new SpySessionTraceService();
        var detector = new TranscriptWakePhraseDetector(sessionTrace: spy);

        detector.TryDetectWake("hey Larry what's up", "Leroy", out _);

        spy.Events.Should().ContainSingle(e => e.EventName == "audio.wake.fuzzy_match");
        var evt = spy.Events.First(e => e.EventName == "audio.wake.fuzzy_match");
        evt.Payload!["matched"].Should().Be("True");
        evt.Payload["phrase"].Should().NotBeEmpty();
    }

    [Fact]
    public void WakeDetector_DoesNotEmit_OnNoMatch()
    {
        var spy = new SpySessionTraceService();
        var detector = new TranscriptWakePhraseDetector(sessionTrace: spy);

        detector.TryDetectWake("hello world", "Leroy", out _);

        spy.Events.Should().NotContain(e => e.EventName == "audio.wake.fuzzy_match");
    }

    // ── SfxPlayer ─────────────────────────────────────────────────────
    // SfxPlayer uses platform-specific AVFoundation on MacCatalyst.
    // The #else stub path emits a trace event on non-MacCatalyst.
    // On net8.0 test target, we get the stub path.

    [Fact]
    public async Task SfxPlayer_EmitsPlayed_OnPlayback()
    {
        var spy = new SpySessionTraceService();
        var player = new SfxPlayer(sessionTrace: spy);

        await player.PlayAsync("test-sound.wav");

        spy.Events.Should().ContainSingle(e => e.EventName == "audio.sfx.played");
        var evt = spy.Events.First(e => e.EventName == "audio.sfx.played");
        evt.Payload!["file_name"].Should().Be("test-sound.wav");
    }

    // ── BrainRequestChannel ──────────────────────────────────────────

    [Fact]
    public async Task BrainRequestChannel_EmitsWrite_OnWriteAsync()
    {
        var spy = new SpySessionTraceService();
        var channel = new BrainRequestChannel(sessionTrace: spy);
        var request = new BrainRequest
        {
            UserQuestion = "What's the best move?",
            LikelyCapability = "get_best_move"
        };

        await channel.WriteAsync(request);

        spy.Events.Should().ContainSingle(e => e.EventName == "brain.request_channel.write");
        var evt = spy.Events.First(e => e.EventName == "brain.request_channel.write");
        evt.Payload!["request_id"].Should().Be(request.RequestId.ToString());
        evt.Payload["capability"].Should().Be("get_best_move");
    }

    [Fact]
    public async Task BrainRequestChannel_EmitsRead_OnTryRead()
    {
        var spy = new SpySessionTraceService();
        var channel = new BrainRequestChannel(sessionTrace: spy);
        var request = new BrainRequest
        {
            UserQuestion = "What's the best move?",
            LikelyCapability = "analyze_position_engine"
        };
        await channel.WriteAsync(request);

        channel.TryRead(out var read);

        spy.Events.Should().Contain(e => e.EventName == "brain.request_channel.read");
        var evt = spy.Events.First(e => e.EventName == "brain.request_channel.read");
        evt.Payload!["request_id"].Should().Be(request.RequestId.ToString());
        evt.Payload.Should().ContainKey("wait_ms");
    }

    [Fact]
    public void BrainRequestChannel_DoesNotEmitRead_WhenEmpty()
    {
        var spy = new SpySessionTraceService();
        var channel = new BrainRequestChannel(sessionTrace: spy);

        channel.TryRead(out _);

        spy.Events.Should().NotContain(e => e.EventName == "brain.request_channel.read");
    }

    [Fact]
    public async Task BrainRequestChannel_Write_NullCapability_EmitsUnknown()
    {
        var spy = new SpySessionTraceService();
        var channel = new BrainRequestChannel(sessionTrace: spy);
        var request = new BrainRequest { UserQuestion = "Hi" };

        await channel.WriteAsync(request);

        var evt = spy.Events.First(e => e.EventName == "brain.request_channel.write");
        evt.Payload!["capability"].Should().Be("unknown");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Wave 2: Replay Analysis Pipeline
    // ═══════════════════════════════════════════════════════════════════

    // Note: GeminiVideoClient, VideoAnalysisTool, and SqliteSegmentAnalysisStore
    // tests use constructor injection. The actual HTTP calls are not made —
    // we verify that the trace service receives the expected events.

    // ── SqliteSegmentAnalysisStore ────────────────────────────────────

    [Fact]
    public async Task SegmentAnalysisStore_EmitsIngested_OnIngest()
    {
        var spy = new SpySessionTraceService();
        var dbPath = Path.Combine(Path.GetTempPath(), $"tel-test-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteSegmentAnalysisStore(dbPath, sessionTrace: spy);
            var result = new VideoAnalysisResult
            {
                SegmentId = "session1-0",
                SessionId = "session1",
                StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
                EndUtc = DateTimeOffset.UtcNow,
                RawJson = "{}",
                NarrativeSummary = "Test summary",
                Beats = new List<AnalyzedBeat>
                {
                    new() { StartTime = "0:00", EndTime = "0:03", Assessment = "opening" }
                }
            };

            await store.IngestAsync(result);

            spy.Events.Should().ContainSingle(e => e.EventName == "replay.store.ingested");
            var evt = spy.Events.First(e => e.EventName == "replay.store.ingested");
            evt.Payload!["segment_id"].Should().Be("session1-0");
            evt.Payload["beat_count"].Should().Be("1");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SegmentAnalysisStore_EmitsSearched_OnSearch()
    {
        var spy = new SpySessionTraceService();
        var dbPath = Path.Combine(Path.GetTempPath(), $"tel-test-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteSegmentAnalysisStore(dbPath, sessionTrace: spy);
            // Ingest data first so search has something to hit
            await store.IngestAsync(new VideoAnalysisResult
            {
                SegmentId = "s1-0",
                SessionId = "s1",
                StartUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
                EndUtc = DateTimeOffset.UtcNow,
                RawJson = "{}",
                NarrativeSummary = "Player captured the pawn on e5",
                Beats = new List<AnalyzedBeat>
                {
                    new() { StartTime = "0:00", EndTime = "0:03", Assessment = "pawn capture on e5" }
                }
            });

            await store.SearchAsync("pawn");

            spy.Events.Should().Contain(e => e.EventName == "replay.store.searched");
            var evt = spy.Events.First(e => e.EventName == "replay.store.searched");
            evt.Payload!["query"].Should().Be("pawn");
            evt.Payload.Should().ContainKey("hit_count");
            evt.Payload.Should().ContainKey("duration_ms");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Wave 3: Persistence + Packs
    // ═══════════════════════════════════════════════════════════════════

    // ── GameSkillPackService ──────────────────────────────────────────

    [Fact]
    public void GameSkillPackService_EmitsLoaded_OnLoadPack()
    {
        var spy = new SpySessionTraceService();
        var packsDir = CreateTempPackFixture();
        try
        {
            var service = new GameSkillPackService(packsDir, sessionTrace: spy);
            service.LoadPack("test-pack");

            spy.Events.Should().Contain(e => e.EventName == "game.pack.loaded");
            var evt = spy.Events.First(e => e.EventName == "game.pack.loaded");
            evt.Payload!["pack_id"].Should().Be("test-pack");
            evt.Payload.Should().ContainKey("game_type");
        }
        finally { Directory.Delete(packsDir, true); }
    }

    [Fact]
    public void GameSkillPackService_EmitsActivated_OnSetActivePack()
    {
        var spy = new SpySessionTraceService();
        var packsDir = CreateTempPackFixture();
        try
        {
            var service = new GameSkillPackService(packsDir, sessionTrace: spy);
            service.SetActivePack("test-pack");

            spy.Events.Should().Contain(e => e.EventName == "game.pack.activated");
            var evt = spy.Events.First(e => e.EventName == "game.pack.activated");
            evt.Payload!["pack_id"].Should().Be("test-pack");
            evt.Payload.Should().ContainKey("agent_name");
        }
        finally { Directory.Delete(packsDir, true); }
    }

    [Fact]
    public void GameSkillPackService_DoesNotEmit_WhenPackNull()
    {
        var spy = new SpySessionTraceService();
        var service = new GameSkillPackService("/nonexistent/path", sessionTrace: spy);

        service.LoadPack("no-such-pack");

        spy.Events.Should().NotContain(e => e.EventName == "game.pack.loaded");
    }

    [Fact]
    public void GameSkillPackService_DoesNotEmit_OnCachedLoad()
    {
        var spy = new SpySessionTraceService();
        var packsDir = CreateTempPackFixture();
        try
        {
            var service = new GameSkillPackService(packsDir, sessionTrace: spy);
            service.LoadPack("test-pack"); // First load — emits
            var firstCount = spy.Events.Count(e => e.EventName == "game.pack.loaded");
            service.LoadPack("test-pack"); // Cached — should not emit again

            spy.Events.Count(e => e.EventName == "game.pack.loaded").Should().Be(firstCount);
        }
        finally { Directory.Delete(packsDir, true); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Cross-cutting: No secrets in any new trace events
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AllNewEvents_ContainNoSecrets()
    {
        var spy = new SpySessionTraceService();

        // Exercise Wave 1 services
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.ExchangeActive),
            sessionTrace: spy);
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ImageAnalysis);

        var queue = new ReminderQueue(sessionTrace: spy);
        queue.Enqueue(MakeReminder("test content", BargeInCategory.CallOut));

        var wakeDetector = new TranscriptWakePhraseDetector(sessionTrace: spy);
        wakeDetector.TryDetectWake("hey Leroy", "Leroy", out _);

        var channel = new BrainRequestChannel(sessionTrace: spy);
        channel.WriteAsync(new BrainRequest { UserQuestion = "test" }).GetAwaiter().GetResult();

        // Verify no secrets in any payload
        foreach (var evt in spy.Events)
        {
            if (evt.Payload == null) continue;
            foreach (var kvp in evt.Payload)
            {
                kvp.Value.Should().NotContain("sk-", $"event '{evt.EventName}' key '{kvp.Key}' may contain a secret");
                kvp.Value.Should().NotContain("APIKEY", $"event '{evt.EventName}' key '{kvp.Key}' may contain a secret");
                kvp.Value.Should().NotContain("Bearer", $"event '{evt.EventName}' key '{kvp.Key}' may contain a secret");
                kvp.Value.Should().NotContain("generativelanguage.googleapis.com",
                    $"event '{evt.EventName}' key '{kvp.Key}' may leak API resource URI");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Event naming convention verification
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AllNewEvents_FollowDotNotation()
    {
        var spy = new SpySessionTraceService();

        // Generate representative events from each service
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.ExchangeActive), sessionTrace: spy);
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ImageAnalysis);

        var queue = new ReminderQueue(sessionTrace: spy);
        queue.Enqueue(MakeReminder("test", BargeInCategory.Reminder));
        queue.Dequeue();

        var detector = new UserSpeechDetector(sessionTrace: spy);
        detector.OnLevelChanged(0.05f);

        var tracker = new AgentSpeechTracker(sessionTrace: spy);
        tracker.OnAudioReceived();

        var wakeDetector = new TranscriptWakePhraseDetector(sessionTrace: spy);
        wakeDetector.TryDetectWake("hey Leroy", "Leroy", out _);

        foreach (var evt in spy.Events)
        {
            evt.EventName.Should().MatchRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$",
                $"event '{evt.EventName}' should follow category.action dot notation");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Payloads are flat dictionaries (Supabase-row-compatible)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AllNewEvents_HaveFlatPayloads()
    {
        var spy = new SpySessionTraceService();
        var gate = new VoiceDeliveryGate(
            new StubExchangeManager(ExchangeState.ExchangeActive), sessionTrace: spy);
        gate.ShouldDeliver(BrainResultPriority.WhenIdle, BrainResultType.ImageAnalysis);

        var queue = new ReminderQueue(sessionTrace: spy);
        queue.Enqueue(MakeReminder("test", BargeInCategory.Reminder));

        foreach (var evt in spy.Events)
        {
            if (evt.Payload == null) continue;
            foreach (var kvp in evt.Payload)
            {
                kvp.Key.Should().NotBeNullOrEmpty();
                // Values should be simple strings (flat, not nested JSON)
                kvp.Value.Should().NotStartWith("{", $"payload key '{kvp.Key}' should not contain nested JSON");
                kvp.Value.Should().NotStartWith("[", $"payload key '{kvp.Key}' should not contain nested JSON");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static ReminderItem MakeReminder(string content, BargeInCategory category,
        DateTime? createdAt = null)
    {
        return new ReminderItem
        {
            Content = content,
            Category = category,
            CreatedAtUtc = createdAt ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a minimal self-contained pack fixture on disk so tests don't depend
    /// on MAUI resource bundling (which only happens during app build, not test build).
    /// </summary>
    private static string CreateTempPackFixture()
    {
        var packsDir = Path.Combine(Path.GetTempPath(), $"tel-packs-{Guid.NewGuid():N}");
        var packDir = Path.Combine(packsDir, "test-pack");
        Directory.CreateDirectory(packDir);

        var manifest = """
            {
                "id": "test-pack",
                "name": "Test Pack",
                "genre": "test",
                "brainInstructions": "brain-instructions.md",
                "observationSchema": {
                    "schemaName": "test_schema",
                    "fields": [
                        { "key": "action", "description": "What happened" }
                    ]
                },
                "eventMapping": [
                    { "field": "action", "eventType": "Assessment" }
                ]
            }
            """;
        File.WriteAllText(Path.Combine(packDir, "pack.json"), manifest);
        File.WriteAllText(Path.Combine(packDir, "brain-instructions.md"), "Test brain instructions");

        return packsDir;
    }

    // ── Spy trace service ─────────────────────────────────────────────

    private sealed class SpySessionTraceService : ISessionTraceService
    {
        private readonly object _lock = new();
        private readonly List<TraceEvent> _events = new();

        public IReadOnlyList<TraceEvent> Events { get { lock (_lock) return _events.ToList(); } }

        public string? RunId => "test-run";
        public string? SessionId => "test-session";

        public void StartRun() { }
        public void EndRun() { }
        public void StartSession() { }
        public void EndSession() { }

        public void TrackEvent(string eventName, Dictionary<string, string>? payload = null)
        {
            lock (_lock)
            {
                _events.Add(new TraceEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    RunId = RunId,
                    SessionId = SessionId,
                    EventName = eventName,
                    Payload = payload
                });
            }
        }

        public void TrackError(string message, string source, Dictionary<string, string>? extra = null)
        {
            var payload = new Dictionary<string, string> { ["message"] = message, ["source"] = source };
            if (extra != null) foreach (var kvp in extra) payload[kvp.Key] = kvp.Value;
            TrackEvent("error", payload);
        }

        public void TrackSessionResult(bool success, string? error = null)
        {
            TrackEvent(success ? "session.connect.success" : "session.connect.failure",
                error != null ? new Dictionary<string, string> { ["error"] = error } : null);
        }

        public void Flush() { }
        public void Dispose() { }
    }

    // ── Stubs ─────────────────────────────────────────────────────────

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
        public StubBargeInPolicy(bool enabled) => _enabled = enabled;
        public BargeInPolicy CurrentPolicy => new() { IsEnabled = _enabled };
        public bool IsBargeInEnabled => _enabled;
        public void SetEnabled(bool enabled) { }
        public void SetCategoryEnabled(BargeInCategory category, bool enabled) { }
        public bool IsCategoryAllowed(BargeInCategory category) => _enabled;
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
