# Game Ghost — Progress Log

**Project:** Game Ghost (.NET MAUI desktop)
**Version:** 1.0.0-beta
**Last Updated:** April 6, 2026

> **2026-04-06 Full gaimer-v2 Overlay Sync.** Replaced cherry-pick sync model with complete source overlay from gaimer-v2 @ `5992eea`. 585 files changed, +105,260/-2,977 lines. Brings in: game skill packs, replay recording, audio intelligence (Phase 12), voice grounding coordinator, settings page, onboarding flow, 1500+ tests, deep codebase audit fixes (22 issues across 26 files). **Game Ghost branding applied:** ApplicationTitle → "Game Ghost", ApplicationId → `com.5dof.gameghost`, AssemblyName → `GameGhost`, UI titles updated (AppShell, MainPage, MinimalView, Onboarding, AgentSelection, DevLauncher), Dross prompt identity → "AI copilot built into Game Ghost", OpenRouter X-Title → "Game Ghost", speech usage description updated, test assertion updated. Native framework names (`GaimerGhostMode`, `GaimerScreenCapture`, `GaimerSpeech`) preserved for runtime interop. 8 macOS Finder duplicate files cleaned. Gaimer remote path updated to `/Users/tonynlemadim/Developer/gAImer_desktop`. **Build:** 0 errors, 22 pre-existing warnings. Output: `GameGhost.dll` with bundle ID `com.5dof.gameghost`. **Commits:** `6c289a7` (overlay), `47840c8` (merge to main). Pushed to `origin/main`. Continuity infrastructure bootstrapped (.planning/STATE.md, chronicles/HANDOFF.md, chronicles/OPEN_THREADS.md).

> **2026-04-03 CoD Mock Service Live Test — First Run.** Demo brain (DemoBrainService_Shoothouse) deployed via `#if DEBUG` swap. RASA agent + CoD HC Cyber Attack pack. **Findings:** (1) GamePacks path broken at runtime — `AppContext.BaseDirectory` → MonoBundle/ but packs in Resources/. ActivePack was NULL since Mar 31. All pack-driven features silently disabled. Fixed with Resources/ fallback. (2) Admission gate keepalive brain starvation — RASA DiffThreshold=255 meant only 1st frame reached brain. Fixed: keepalive path sends to brain when BrainMinInterval elapsed. (3) CaptureEmissionGate blocks byte-identical frames — paused QuickTime produced 1 frame. By design (no bug). Playing video resolved. (4) Empty array timeline cards — `system_alerts:[]` and `player_kills:[]` created blank cards. Fixed: skip zero-length arrays. (5) Ghost card display too fast — assessment cards barely readable before disappearing. Need longer display duration or user-controlled dismiss. (6) Ghost mode received fewer events than timeline — event type restrictions may be too tight. **Metrics (50 captures, ~17 min session):** Recording 28.8-29.3 FPS stable. Managed heap 35→56MB (normal GC). WorkingSet peaked 760MB then dropped to 592MB (healthy). No memory leak pattern. **Pipeline verified:** Capture → EmissionGate → AdmissionGate → DemoBrain (3s delay) → Router → Pack parser → Structured timeline events (Assessment, Opportunity, GameStateChange). Voice narration delivery confirmed. Top strip updated correctly.

> **2026-04-03 Deep Codebase Audit + Adversarial Review.** 22 issues found and fixed across 26 files (+260/-134 lines). **Audit method:** 5 parallel Claude agents (exchange pipeline trace, Phase 12 DI wiring, voice-brain sync, replay+skill packs, dead code scan) + Codex adversarial code review. **Blockers fixed (7):** B1 UpdateInstructionsAsync delegation, B2 AgentSpeechTracker wired into VoiceDeliveryGate, B3 IsDeferredAnswer=true in SubmitQueryAsync, B6 replay StopAsync/CleanupSessionFiles split, B7 exchange close+reminder clear on disconnect, plus BrainEventRouter double-delivery guard (C1) and IsStale boundary fix (C2). **Warnings fixed (8):** Brain receives voice transcripts (W1), time_hint parsing+clamping (W3/C3), search_replay post-game (W4), CoD silence preset (W5), player_history/analytics removed (W6). **Round 2 fixes (4):** Ghost deferred answers (W-new-1), priority reminder dequeue (W-new-2), close SFX suppress on disconnect (W-new-3), replay temp file staging (W-new-4). **Three recurring patterns identified:** (1) adapter doesn't delegate, (2) built but never wired, (3) lifecycle timing. **Tests:** 1507 passed, 18 skipped, 0 failed (net8.0).

> **2026-04-02/03 Phase 12 Live Testing + Bug Fixes.** 14 commits (9a02770..5a3535b), +15 new tests (1514 total passing). Live test checklist created (60+ items, 5 tiers) at `WITNESS/PHASE-12-LIVE-TEST-CHECKLIST.md`. **8 bugs found and fixed:** (1) Personality announcement — anti-pattern rules added to agent prompts. (2) Agent name as greeting — prompt rule added. (3) Chess grounding for non-chess — pack-driven `GroundingLanguage` replaces hardcoded chess terms. (4) Interruption not working — fire `Interrupted` event on `speech_started` server event. (5) Wake word not enforced — prompt-level wake phrase requirement added. (6) No game state awareness — runtime `session.update` on InGame/OutGame state changes. (7) Double output — single `MessageReceived` path eliminates duplicate agent responses. (8) Chess-specific classification — pack-driven `VoiceClassificationPatterns` replaces hardcoded chess patterns. **5 issues still open:** Exchange never fires (wake detection transcripts not reaching fuzzy detector), screen recording FPS degraded to 0.4fps with voice active (was 25fps), RSS climbing toward 500MB (possible memory leak), brain blocked (OpenRouter 402 credits exhausted), Porcupine blocked (Picovoice approval pending). **Tests:** 1499 -> 1514 passed (+15 new), 18 skipped, 0 failed.

> **2026-04-02 Phase 12 Audio Intelligence Pipeline — COMPLETE.** All 5 sub-phases (12A-12E) delivered in a single extended session. 31 commits (4e64d23..ef9d9d4), +179 new tests (1499 total passing, 18 skipped). 4 adversarial code reviews: 5 criticals + 16 warnings found and fixed. All 9 design decisions enforced (D-AI-1 through D-AI-9). **What ships:** Exchange-gated voice (wake phrase opens, silence timer closes, connection warm), Porcupine wake word (pending key) + fuzzy Levenshtein fallback, barge-in control (user toggle, 4 categories, D-AI-4 user-speaking guard), reminder queue (bounded, stale-pruning, surfaces at next exchange), voice-brain bridge (deferral flow, stock ack, priority channel), audio tracking (agent speech gap-based, user speech threshold+debounce), SFX wake ping (25%), graceful degradation (Full/VoiceOnly/TextOnly), per-game silence presets (Quick 8s / Normal 15s / Patient 30s), exchange lifecycle telemetry, 8 integration tests. **Known blockers:** Picovoice account approval pending (6 tests skipped), V1 text-prefix-only brain request correlation, 3 deferred review items (W1 DI order, W2 HashSet thread safety, W3 ghost mode sync gap). **Sub-phase breakdown:** 12A (7 commits, +52 tests: exchange state machine, VoiceDeliveryGate, wake phrase), 12B (8 commits, +48 tests: audio tracking, Porcupine, SFX, fuzzy wake), 12C (6 commits, +31 tests: barge-in policy, reminder queue, decision matrix), 12D (5 commits, +29 tests: voice-brain bridge, turn classification, deferral), 12E (3 commits, +19 tests: degradation modes, silence presets, telemetry, integration tests), review fixes (2 commits: 5 criticals fixed).

> **2026-04-02 Phase 12E — Polish + Settings + Degradation (Audio Intelligence Pipeline).** Final sub-phase. 3 commits (d202473..ef9d9d4), +19 new tests. **AudioIntelligenceMode enum + SilenceTimeoutPreset:** Per-game silence presets (Quick 8s / Normal 15s / Patient 30s) added to GameSkillPack. ExchangeContext added to SharedContextEnvelope for exchange-aware brain context. FreshnessRules added to Agent model. 6 tests. **Graceful degradation modes:** AudioIntelligenceMode.Full/VoiceOnly/TextOnly with mode selection based on available audio dependencies. Exchange lifecycle telemetry (opened/closed/wake events emitted via session trace). 5 tests. **Integration test suite:** Full exchange lifecycle (wake → active → silence → close), barge-in matrix verification, reminder surfacing on exchange open, degradation mode transition testing. 8 tests. **Tests:** 1480 -> 1499 passed (+19 new), 18 skipped, 0 failed.

> **2026-04-02 Phase 12D — Voice-Brain Request Bridge (Audio Intelligence Pipeline).** Subagent-driven TDD with adversarial code review. 5 commits (d1692d0..ecec979), +29 new tests. **BrainRequest + BrainCapabilityManifest:** Request model for voice-to-brain deferral with priority, category, and context fields. Manifest for brain capability advertisement. BrainResult extended with DeferredRequestId and DeferredBrainResponse fields. **Turn classification expansion:** HistorySensitive, ToolDependent, DeferToBrain categories added to classify turns that require brain involvement. FormatFreshnessPrefix utility for brain context freshness tagging ("FRESH:", "WARM:", "STALE:"). 23 tests. **IBrainRequestChannel + BrainRequestChannel:** Bounded voice-to-brain priority channel with reader/writer separation (Channel<BrainRequest> bounded to 5). 5 tests. **Voice-brain deferral flow:** Voice pipeline detects DeferToBrain turns, submits BrainRequest via channel, brain consumer picks up request, analyzes, and delivers tagged response back through existing BrainResult pipeline. Brain request channel registered in DI. **Adversarial review:** C1 critical (PendingCount tracked via separate int counter diverged from actual channel count — fixed to use Reader.Count as single source of truth), W1 (consumer cancellation token propagation), W2 (AwaitingBrain timer reset on deferral — exchange stays open while brain processes), W3 (gate bypass documentation for brain-initiated responses that skip VoiceDeliveryGate). **V1 limitation documented:** No DeferredRequestId correlation in SubmitQueryAsync — brain receives text prefix hint only (e.g., "[DEFERRED:abc123] ..."). Full request-response correlation deferred to V2. **Session totals (12A+12B+12C+12D):** 28 commits (4e64d23..ecec979), +160 new tests, 4 adversarial reviews, 5 criticals + 16 warnings found and fixed. **Tests:** 1451 -> 1480 passed (+29 new), 18 skipped, 0 failed.
>
> **New files (~4):** BrainRequest.cs, BrainCapabilityManifest.cs, IBrainRequestChannel.cs, BrainRequestChannel.cs + test files. **Modified (~6):** BrainResult.cs, VoiceDeliveryGate.cs, ExchangeManager.cs, MainViewModel.cs, MauiProgram.cs, and others.

> **2026-04-02 Phase 12C — Barge-In Policy + Reminder Queue (Audio Intelligence Pipeline).** Subagent-driven TDD with adversarial code review. 6 commits (8e029a4..3575c89), +31 new tests. **Barge-in models:** BargeInCategory enum (GameState, Coaching, Danger, Social, System), BargeInPolicy (per-category allow/suppress), ReminderItem (content + category + timestamp + staleness). **BargeInPolicyService:** IBargeInPolicyService + BargeInPolicyService — user preference persistence for per-category barge-in settings via Preferences, 8 tests. **ReminderQueue:** IReminderQueue + ReminderQueue — bounded (10 items), staleness-based expiry, supersession by category, Dequeue returns oldest-first, 9 tests. **VoiceDeliveryGate 7-path barge-in matrix:** D-AI-4 user-speaking guard, BargeInCategory mapping from DeliveryDecision, reminder queueing when suppressed, 12 tests. **UI integration:** Barge-in toggle replaces GAME AUDIO in ghost panel, reminder surfacing on exchange open via ExchangeManager.StateChanged subscription, DI registration for all new services, D-AI-5 integration test. **Adversarial review:** C1 critical (proactive alerts silently dropped when barge-in suppressed — now route through QueueReminder path) + W4 (Supersede for category replacement in ReminderQueue). W1-W3 deferred (priority ordering, Settings UI, concurrent delivery tests). Design decisions enforced: D-AI-4 (barge-in only when user silent), D-AI-5 (interrupted thoughts decay via staleness + reminder queue). **Session totals (12A+12B+12C):** 23 commits (4e64d23..3575c89), +131 new tests, 3 adversarial reviews, 4 criticals + 13 warnings found and fixed. **Tests:** 1420 -> 1451 passed (+31 new), 18 skipped, 0 failed.
>
> **New files (~8):** BargeInCategory.cs, BargeInPolicy.cs, ReminderItem.cs, IBargeInPolicyService.cs, BargeInPolicyService.cs, IReminderQueue.cs, ReminderQueue.cs + test files. **Modified (~5):** VoiceDeliveryGate.cs, MainViewModel.cs, MauiProgram.cs, and others.

> **2026-04-02 Phase 12B — VAD / Echo / Mic Management + Porcupine + SFX Player.** 10 commits (9eba8a1..922defd), ~48 new tests. **AgentSpeechTracker:** gap-based agent speech detection (500ms silence gap). **UserSpeechDetector:** threshold-based user speech detection (0.02 RMS, 300ms debounce). **ghost_panel_set_exchange_state:** P/Invoke + IGhostModeService.SetExchangeState for native VAD animation bridging (D-AI-9). **Fuzzy wake phrase upgrade:** TranscriptWakePhraseDetector upgraded from regex to 3-tier fuzzy Levenshtein matching (exact → sliding window pairs → merged single-word, DefaultMaxDistance=2). **Platform parity:** docs/platform-parity/audio-input-windows.md documents Windows audio input strategy (D-AI-8). **MainViewModel wiring:** AgentSpeechTracker → AudioReceived, UserSpeechDetector → VolumeChanged, ExchangeStateChanged → ghost, UserSpeechStarted → exchange. DI for IAgentSpeechTracker, IUserSpeechDetector. **Adversarial review:** C1 (fuzzy false positives for short names at distance 3 — capped), C2 (UserSpeechDetector missing IDisposable — added) + W1-W3 fixed. Design decisions enforced: D-AI-2 (VAD/echo), D-AI-8 (Windows parity), D-AI-9 (VAD display animation). **Porcupine wake word:** IPorcupineWakeDetector + PorcupineWakeDetector (Picovoice NuGet 4.0.2, PCM frame buffering, graceful degradation, PICOVOICE_ACCESS_KEY env var). BLOCKED on Picovoice Console account approval — 6 live detection tests skipped. Wake strategy: Porcupine primary (raw audio, offline, <5ms) → fuzzy transcript fallback. **SFX player:** ISfxPlayer + SfxPlayer (AVAudioPlayer, MacCatalyst), affirmation_ping.mp3 (2.09s, 81KB, 25% volume on ExchangeOpened). Ready for 12C barge-in warning reuse. **Lesson learned:** always verify external dependency signup/licensing before implementing. **Tests:** 1372 -> ~1420 passed (+~48 new), 18 skipped (12 pre-existing + 6 Porcupine), 0 failed.
>
> **New files (~16):** AgentSpeechTracker.cs, IAgentSpeechTracker.cs, UserSpeechDetector.cs, IUserSpeechDetector.cs, PorcupineWakeDetector.cs, IPorcupineWakeDetector.cs, SfxPlayer.cs, ISfxPlayer.cs, audio-input-windows.md, affirmation_ping.mp3 + ~6 test files. **Modified (~8):** TranscriptWakePhraseDetector.cs, MainViewModel.cs, MauiProgram.cs, IGhostModeService.cs, NativeMethods.cs, WitnessDesktop.csproj, and others.

> **2026-04-02 Phase 12A — Exchange State Machine + Wake Phrase Detection (Audio Intelligence Pipeline).** Subagent-driven TDD with adversarial code review. 7 commits (4e64d23..33de337), +52 new tests. **Exchange state machine:** ExchangeState enum (8 states: Idle, Waking, Active, AgentSpeaking, Closing, Paused, Errored, Silent), ExchangeSession immutable snapshot record, IExchangeManager interface, ExchangeManager with silence timer (default 15s, D-AI-3). **Wake phrase detection:** TranscriptWakePhraseDetector with regex "Hey {AgentName}" (D-AI-7), compiled regex cache per agent name. **Voice delivery gate:** VoiceDeliveryGate with DeliveryDecision enum, central exchange-aware voice gating, Silent priority always suppresses. **BrainEventRouter gating:** All 4 SendContextualUpdateAsync calls (OnBrainHint, OnProactiveAlert, RouteBrainResult voice path) gated through VoiceDeliveryGate. **MainViewModel wiring:** Wake detection in UserTranscriptReceived, agent speech tracking, DI registration. **Adversarial review:** C1 critical fix (RouteBrainResult — the primary Channel consumer data path — was ungated), W2 (atomic lock in OnWakeDetected), W3 (NRE race fix), W4 (Silent priority leak), W5 (regex caching), W6 (missing timer test). 5 notes documented (unused param, record staleness, state collapsing, test gap, forward declaration). **New files (12):** ExchangeState.cs, ExchangeSession.cs, IExchangeManager.cs, ExchangeManager.cs, IWakePhraseDetector.cs, TranscriptWakePhraseDetector.cs, IVoiceDeliveryGate.cs, VoiceDeliveryGate.cs + 4 test files. **Modified:** BrainEventRouter.cs, MainViewModel.cs, MauiProgram.cs. Design decisions enforced: D-AI-1 (warm pause), D-AI-3 (silence timer), D-AI-7 (regex wake phrase). **Tests:** 1320 -> 1372 passed (+52 new), 12 skipped, 0 failed.
>
> **Modified:** `Services/Brain/BrainEventRouter.cs`, `ViewModels/MainViewModel.cs`, `MauiProgram.cs`, new: `Models/Exchange/ExchangeState.cs`, `Models/Exchange/ExchangeSession.cs`, `Services/IExchangeManager.cs`, `Services/ExchangeManager.cs`, `Services/Audio/IWakePhraseDetector.cs`, `Services/Audio/TranscriptWakePhraseDetector.cs`, `Services/IVoiceDeliveryGate.cs`, `Services/VoiceDeliveryGate.cs`

> **2026-04-02 Replay Recording Pipeline (Phases 1-3) + Audio Intelligence Design Lock + Doc-Sync Skill.** Two sessions. Session 1: Full replay pipeline delivered end-to-end in 10 commits (8fdcd75..64963ed), +43 new tests. **Phase 1 (Recording):** Manually verified — FPS 25-29fps, HEVC hvc1, 2:30 rotation, fragmented MP4, 1920x1080, ~89MB/segment. **Phase 2 (Video Analysis):** 7 commits. GeminiVideoClient (upload/poll/generate/cleanup via Gemini Files API), SqliteSegmentAnalysisStore (FTS5 full-text search), VideoAnalysisTool (pack-driven prompts, circuit breaker), ReplayAnalysisOrchestrator (Channel consumer). Adversarial review: C1-C3, W1, W3, W5, W6 fixed. **Phase 3 (Agent Search):** 2 commits. search_replay ToolDefinition (query + time_hint), two-tier ToolExecutor (FTS5 → Gemini Pro fallback), all 3 agents whitelisted, SessionManager InGame registration. **Prompt tuning:** REPLAY SEARCH decision trees for chess + RASA agents. GEMINI_API_KEY env var fallback. **Audio Intelligence:** Phase 12 roadmap committed (5 sub-phases, 39 tasks, adversarial-reviewed). Exchange Protocol spec extended (9 design decisions D-AI-1 through D-AI-9 locked). Session 2: Created `/doc-sync` skill (5-tier document manifest, 7 cross-reference rules, Knowledge Vault integration). Fixed STATE.md Phase 12 status from stale "Research Required". Full chronicler run across all tiers. **Tests:** 1320 passed, 12 skipped, 0 failed.
>
> **Modified (Session 1):** `MauiProgram.cs`, `Models/Agent.cs`, `Models/ToolDefinition.cs`, `Services/Brain/ToolExecutor.cs`, `Services/SessionManager.cs`, new: `Services/Replay/GeminiVideoClient.cs`, `Services/Replay/SqliteSegmentAnalysisStore.cs`, `Services/Replay/VideoAnalysisTool.cs`, `Services/Replay/ReplayAnalysisOrchestrator.cs`, `Services/Replay/VideoAnalysisModels.cs`. Tests: `SearchReplayToolTests.cs`, `SessionManagerTests.cs`, `ToolDefinitionTests.cs`. Docs: `AUDIO_INTELLIGENCE_EXCHANGE_PROTOCOL.md`, Phase 12 roadmap, Phase 1-3 plans.

> **2026-03-31 Game Skill Packs.** Game-agnostic brain pipeline via loadable packs. 8 tasks, 8 commits (ce0cc7e..66e964e), 24 new tests. Chess migrated as reference pack (JSON manifest + markdown). CoD HC Cyber Attack as pack #2. BrainPromptBuilder, OpenRouterClient, BrainEventRouter all driven by active pack. MainViewModel.OnSelectedAgentChanged wires SetActivePack. **Tests:** 1277 passed, 12 skipped, 0 failed.

> **2026-03-28 V2 Codebase Audit + Phase 07 Persistence Layer.** Full audit of 13 open threads + codebase health scan. 3 threads closed (T1-H3, T1-M5, T7), 2 re-scoped (T-TELEMETRY, T21). C1: demo code guarded with `#if DEBUG`. C2: legacy IGeminiService removed. Phase 07 complete — 4 plans, 3 waves. EF Core + SQLite for session/chat/timeline history. ReplayRetrievalService + ReplayAnchorService for time-window + anchor queries. BrainPromptBuilder.AssembleReplayContext for LLM enrichment. Codex-reviewed: 5 warnings fixed. 13 race-condition integration tests. **Tests:** 1164 → 1277 passed.

> **2026-03-27 Voice Transcript Bridge (T-VOICE-TRANSCRIPT).** IVoiceTranscriptStore ring buffer (5-min, 100-turn cap), UserTranscriptReceived event (OpenAI only), SharedContextEnvelope.RecentVoiceTranscript with budget-aware assembly, transcript telemetry. Brain now sees what user said to voice. New-game flush wired. Code-reviewed: 1 important issue found and fixed. **Tests:** 1148 → 1164 passed (+16 new).

> **2026-03-26 BUG-020 + T17 RASA + GhostFab Token Refactor.** BUG-020: HandleTerminalBrainErrorAsync now calls CancelAll() only — brain pauses, connector stays up. Ghost toggle visual reset fixed (audio diff-guard). T17: Derek retired, RASA registered at "general" key with full 5-block personality, VoiceId="echo", agent-aware tool filtering, generalized game_journal. GhostFab token refactor: 10 nested enums. **Tests:** 1124 → 1148 passed (+24 new).

> **2026-03-25 BUG-017 + BUG-018 + BUG-019.** BUG-017: ChatCompletionWithRetryAsync (exponential backoff, 4 retries), BrainRequestFailedException, error dedup, TerminalBrainErrorReceived auto-disconnect. Code-reviewed by 5 parallel agents. BUG-018: ShouldIgnoreGhostAudioToggle 250ms dedup + pending guard. BUG-019: ShowUnsupportedAudioFeedback routes to ghost card when ghost mode active. **Tests:** 1112 → 1124 passed (+12 new).

> **2026-03-24 Ghost-Mode Fix Set + GhostFab-Codex Transition.** Wrong-monitor pre-positioning, emission queue append-not-clear, host-window resolveHostWindow, notification rotation loop. Old circular FAB + 7 legacy Swift views replaced with Codex panel stack. NSBundle.module crash fixed via GhostFabResources.swift. ICON-001 closed. Live verified. **Tests:** 1110 → 1112 passed. Net: +3002 / -2068 lines across 48 files.

> **2026-03-23 GhostFab-AppKit Phase 7 + Gaimer Integration.** 24 package tests, standalone harness app, interaction diagnostics, code-reviewed against spec. Compatibility framework mounted, contract verification, sync script, defensive toggle handling. **Tests:** 1107 → 1110 passed.

> **2026-03-21 Performance Hardening Sprint (D-038 through D-048).** Memory profiling revealed CoreAnimation layer buildup (182MB), main thread saturation from preview dispatch on every capture tick, and zero-gap brain request chaining. Session delivered 11 decisions and dropped physical footprint from 550MB to 351MB, CA dirty from 182MB to 38MB, CPU steady state from 42-95% to 2.9-8%. **Emission queue (D-040):** `BrainEventRouter.OnStructuredAnalysis` now enqueues events instead of emitting directly. A `PeriodicTimer`-based loop drip-feeds events to the timeline at 2.5s intervals. New batch supersedes old (queue cleared, already-displayed events stay). Priority order: Danger → Assessment → SageAdvice (D-041, D-042). **ImageAnalysis routing (D-039):** ImageAnalysis (model "thinking") excluded from timeline and ghost — routes to TopStrip live activity bar only. Both structured and unstructured fallback paths updated. **Preview gating (D-038):** Preview image updates only for admitted frames — rejected frames produce zero main-thread dispatches. **Brain pacing gate:** 3s SemaphoreSlim cooldown on both auto-capture and on-demand paths. **T18 bounded retention (D-043):** 5-minute wall-clock pruning with 60s sweep timer. CA layers dropped from 182MB to 38MB. Archive boundary marker stays at cutoff (D-044). **Preview hidden in ghost mode (D-045):** DataTrigger hides preview when ghost mode active. **Template simplification (D-046, D-047):** Event icon moved outside bubble, collapsed events rendered as small text capsules with tighter spacing. **Newest-first ordering (D-048):** Newest event leads (leftmost) in horizontal compressed EventLines. **Ghost/FAB fix:** `AnalysisEventEmitted` event on `IBrainEventRouter` fires from `EmitNextQueued()`. MainViewModel subscribes and forwards to `ShowCard()` for ghost mode overlay. **Capture cadence:** 5s default re-applied, preview-first diff, rebuilt xcframework. **GhostFab native rebuild spec:** Written for off-site AppKit NSPanel implementation at `docs/superpowers/specs/2026-03-21-ghostfab-native-rebuild-spec.md`. **Tests:** 1107 passed, 12 skipped, 0 failed (+17 from 1090 baseline).
>
> **Modified:** `BrainEventRouter.cs`, `IBrainEventRouter.cs`, `MainViewModel.cs`, `MainPage.xaml`, `TimelineView.xaml`, `TimelineFeed.cs`, `ITimelineFeed.cs`, `TimelineCheckpoint.cs`, `TimelineEvent.cs`, `BrainEventRouterTests.cs`, `MainViewModelTests.cs`, `ChannelPipelineTests.cs`, `PipelineIntegrationTests.cs`, `ChatTimelineRoutingTests.cs`

> **2026-03-20 Capture Pressure Reduction + Deploy Entitlements Correction.** Live freeze investigation showed the app was not deadlocked; it was saturating CPU in the capture path. Unified logs showed repeated ScreenCaptureKit shareable-window enumeration and native helper log spam on every capture tick while connected, with Gaimer consuming ~175% CPU during the freeze. Memory profiling from the same investigation reinforced that long-session stability now depends on bounded UI retention (`T18`), reduced capture churn, and stricter separation between observation storage and brain fanout. **Runtime decision:** change the default still-image capture cadence from 30s/1s-polling behavior to a real 5-second baseline, keep `capture_screen` as the explicit on-demand refresh path, and treat the observation store as the primary seam for future still/clip capture instead of raw brain fanout. **Implementation:** `CaptureConfig.CaptureIntervalMs` defaults moved to 5000ms, Mac/Windows/mock capture service defaults updated to 5000ms, and the effective capture poll ceiling was raised from 1s to 5s so the service no longer wakes every second underneath the agent-configured interval. Agent-facing copy now says "approximately every 5 seconds" instead of "whenever the board changes." **Hot-path reduction:** frame diff/admission now runs on the preview-sized JPEG instead of the full analysis artifact, full `ScaleAndCompress` work is only paid for admitted observations, skipped frames no longer create timeline checkpoints or reel entries, and managed per-tick capture logging was trimmed sharply. **Review-driven follow-up:** Windows capture now mirrors the Mac one-shot timer/re-arm pattern so slow ticks do not overlap, Windows no longer pre-compresses frames before emission (the ViewModel owns the only compression path), and the mock capture service now respects the requested interval instead of hardcoding 1 second. **Brain pacing:** OpenRouter image analysis now passes through a shared serialization/cooldown gate so auto-capture and on-demand `SubmitImageAsync` requests cannot chain back-to-back with zero gap; the default minimum interval is 3 seconds in production, and a new unit test verifies the configured cooldown is respected. **Native source follow-up:** the GaimerScreenCapture Swift helper source was reduced to error-only logging to stop `SCShareableContent` / window enumeration log floods; the source change is in repo and needs the native xcframework rebuilt before those native log reductions take effect at runtime. **Deploy finding:** local `/Applications` deploys were carrying an invalid entitlements blob because sandbox-only keys were present in the Developer ID entitlements file; those keys were removed so the local deploy path no longer signs a blob macOS reports it will ignore. **Architectural decision:** future 30s-60s high-speed-agent video should be modeled as `ObservationKind.Clip` under the same observation-store contract, with stills remaining the cheap always-on signal and clips cut selectively from a rolling buffer on salience/tool demand rather than streaming long video continuously to the brain. **Verification:** `dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst` and `dotnet test src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0` both succeeded after the capture-default, Windows follow-up, and brain-pacing changes (`1090 passed / 12 skipped`).
>
> **Modified:** `src/WitnessDesktop/WitnessDesktop/Models/Agent.cs`, `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/WindowCaptureService.cs`, `src/WitnessDesktop/WitnessDesktop/Platforms/Windows/WindowCaptureService.cs`, `src/WitnessDesktop/WitnessDesktop/Services/IWindowCaptureService.cs`, `src/WitnessDesktop/WitnessDesktop/Services/MockWindowCaptureService.cs`, `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`, `src/WitnessDesktop/NativeHelpers/GaimerScreenCapture/Sources/GaimerScreenCapture/GaimerScreenCapture.swift`, `scripts/WitnessDesktop.entitlements`

> **2026-03-19 P0 Capture/Voice Stabilization — Observation Store + Admission Gate + Voice Dedupe.** Post-live-testing stabilization work shifted the runtime architecture away from raw capture fanout and toward a bounded recent-gameplay memory. **Observation store foundation:** added `IObservationStore` with `SqliteObservationStore` using SQLite for metadata/index and filesystem artifact persistence for retained observations. **Admission gate:** added explicit `store_only` vs `store_and_send_to_brain` decisions so accepted capture no longer implies immediate brain pressure on every stored observation. **Grounded voice cooldown:** `BrainEventRouter` now suppresses duplicate grounded idle voice-context pushes within cooldown while preserving interrupt/high-urgency behavior. **Live result:** deployed debug build to `/Applications`, observed calmer runtime behavior, confirmed local observation artifacts under `~/Library/observations`, and got a strong grounded factual board response from voice. **Checkpoint:** one more focused live verification pass is still needed to explicitly confirm reduced grounded voice-context injection noise before closing the current BUG-014 checkpoint.  
>
> **Modified:** `MauiProgram.cs`, `MainViewModel.cs`, `BrainEventRouter.cs`, `WitnessDesktop.csproj`, new observation-store/admission models and services, focused tests for storage/admission/router dedupe

> **2026-03-18 Release Script Fixes — Permissions & Architecture Detection.** Investigated app launch failure where screen capture permissions were denied as if unsigned. **Root cause #1 (permissions):** Stale TCC (Transparency, Consent, and Control) denials cached from a previous session — macOS remembered a "Deny" for `com.5dof.gaimer` screen capture. Reset via `tccutil reset ScreenCapture` and `tccutil reset All com.5dof.gaimer`. App launched clean with fresh permission prompts. **Root cause #2 (architecture):** `build-release-mac.sh` hardcoded arm64-first bundle lookup, but dev machine is Intel (x86_64). Script picked the arm64 `.app` bundle, causing "incorrect executable format" on launch. **Fix:** Detect host arch via `uname -m`, pick matching RID first (`maccatalyst-x64` on Intel, `maccatalyst-arm64` on Apple Silicon). **Root cause #3 (zip signature invalidation):** `ditto -c -k` operating directly on the signed `.app` bundle mutated it during zip creation, invalidating the code signature. **Fix:** Copy to a staging `.app` first, then zip the staging copy — preserves the signed original. **New feature:** `--local-deploy` flag builds, signs with Developer ID, deploys to `/Applications`, verifies signature, and launches — single command for local testing. **Icon recovery:** `dotnet publish -c Release` strips icon renditions from `Assets.car` (compiled asset catalog). Script now copies `Assets.car` + `appicon.icns` from debug build and patches `CFBundleIconFile`/`CFBundleIconName` into Info.plist. **Known bug (tracked):** Release bundle app icon still does not display in Dock despite identical `Assets.car` from working debug build. Debug bundle deploys with correct blue "G" icon; release bundle shows default MAUI grid icon. Suspected Mac Catalyst asset catalog loading difference between Debug and Release configurations. Not a signing or caching issue.
>
> **Modified:** `scripts/build-release-mac.sh`

> **2026-03-14 Post-MiniCPM Cloud-First UX Sprint — 8 Slices Delivered.** Massive Codex→Claude rotation session. **Toggle Responsiveness (T15):** IndustrialToggleSwitch gets `IsPending` with pulsing LED animation; voice chat toggle now moves immediately with separate pending indicator; failure snap-back on mic start error. **Timeline Focus Compression (T19, 2 slices):** TimelineEvent gets INotifyPropertyChanged with `IsExpanded`/`IsCollapsed`; newest event auto-expands, older events collapse to 36x36 icon circles; `IsLatest` guard prevents manual collapse of latest; all 3 templates support expanded/collapsed states; default template changed from TailTruncation to WordWrap. **Tool-Call Visibility (T16):** New `EventOutputType.ToolCall` with muted blue treatment; `BrainResult.ToolCalls` carries ToolCallInfo through Channel pipeline; OpenRouterBrainService collects name/duration/success via Stopwatch; dedicated timeline template (14pt, 16px icon). **Ghost-Mode Tool-Use (2 slices):** `ToolCallReceived` event on IBrainEventRouter; `SlidingPanelContent.ToolCall`/`IsToolCall`/`ToolIconPath` drive layout switch; GaimerHudView dedicated inner layout with centered 40x40 icon + action phrase; `HasTextPanelContent` separates tool cards from AI content. **Archived Boundary (T18-boundary, 2 slices):** `TimelineCheckpoint.IsArchiveBoundary` as global end-of-scroll marker; `InsertArchiveBoundary()` appends checkpoint at bottom; truly centered in feed width (checkpoint-level, not inside EventLine grid); grey capsule with clock icon. **Cloud-first cleanup:** Local toggles/diagnostics hidden, persisted state leak fixed. **Infrastructure:** IStructuralSettingsTracker + rebootstrap guard. **Tests:** 901→938 passing, 0 failures (13 pre-existing resolved). **37 new tests total.** MacCatalyst build clean.
>
> **Modified:** IndustrialToggleSwitch.xaml.cs, MainViewModel.cs, TimelineEvent.cs, TimelineFeed.cs, ITimelineFeed.cs, TimelineView.xaml, TimelineEventTemplateSelector.cs, EventOutputType.cs, EventIconMap.cs, BrainResult.cs, BrainEventRouter.cs, IBrainEventRouter.cs, OpenRouterBrainService.cs, SlidingPanelContent.cs, GaimerHudView.xaml, AudioControlPanelView.xaml, SettingsViewModel.cs, SettingsPage.xaml, TimelineCheckpoint.cs, ToolCallInfo.cs, App.xaml.cs, MauiProgram.cs

> **2026-03-13 Local-First Research Concluded — Cloud Path Reinstated.** Phase 17 local inference work produced meaningful infrastructure and bug fixes, but live testing on the current machine established that the LocalOnly path is not a viable product direction right now. **What was proven:** (1) LocalOnly voice routing bug fixed — `ConversationProviderFactory` now hard-locks local voice when `InferenceMode=LocalOnly`, even if `VOICE_PROVIDER=openai`; (2) Mac Catalyst speech-recognition TCC crash fixed by injecting `NSSpeechRecognitionUsageDescription` into the built app bundle plist during Mac Catalyst build; (3) out-game/in-game text display on the visible timeline fixed by routing direct text messages through `BrainEventRouter`; (4) local Ollama requests confirmed working from CLI, but `minicpm-v` on CPU took ~16.4s for a trivial "hello" prompt; (5) local text/vision clients needed timeout expansion from 3m → 10m to avoid false cancellations during test. **What was disproven:** Local voice + local vision + local reasoning is not usable on this hardware for real-time product UX. Voice remained non-viable after TCC was cleared due to poor STT/chunking behavior and near-silent input levels; direct local text remained too slow for acceptable interaction; local vision/capture inference is too latency-sensitive to trust on CPU. **Decision:** LocalFirst research is paused until higher-quality target hardware is available. Active development and live testing rotate back to cloud providers immediately. **Follow-up direction:** Use the Phase 17 work as infrastructure only; do not continue optimizing local inference on this machine. Next focus is cloud provider verification, capture bundling, and settings-triggered app rebootstrap after structural config changes.
>
> **Modified during investigation:** `ConversationProviderFactory.cs`, `ConversationProviderFactoryTests.cs`, `MainPage.xaml`, `MainViewModel.cs`, `IBrainEventRouter.cs`, `BrainEventRouter.cs`, `Platforms/MacCatalyst/Info.plist`, `WitnessDesktop.csproj`, `MauiProgram.cs`, `LocalMiniCpmBrainService.cs`

> **2026-03-10 Phase 16 Complete — Vision Context Engineering (Anti-Hallucination).** Five plans executed with TDD discipline. **Plan 01 (Brain Prompt Engineering):** Chain-of-thought `visual_description` field forces grounding before analysis, UNREADABLE escape hatch prevents confident confabulation, confidence calibration tags (CERTAIN/LIKELY/UNCERTAIN/GUESSING). **Plan 02 (Live Activity Bar Fix):** `TopStripUpdated` event on `IBrainEventRouter`, MainViewModel subscribes with `MainThread.BeginInvokeOnMainThread` — event-based DI decoupling eliminates circular dependency. Fired at all 5 routing paths (OnScreenCapture, OnBrainHint, OnImageAnalysis, OnProactiveAlert, OnGeneralChat). **Plan 03 (Gemini Model Switch):** Brain model changed from `anthropic/claude-sonnet-4` to `google/gemini-2.5-flash`. OpenRouter provider preferences force Google-only routing (`allow_fallbacks: false`). Worker model unchanged (gpt-4o-mini). **Plan 04 (Structured JSON Output):** `BrainAnalysisResult` model with 7 nullable fields. `response_format` JSON schema with `strict: true` on OpenRouterClient. `TryParseStructuredAnalysis` in BrainEventRouter with free-text fallback. L1 confidence now extracted from structured response (was hardcoded 0.8). **Plan 05 (Temporal Consistency):** `ValidateTemporalConsistency` on GameJournalService — duplicate FEN detection + impossible piece count changes (>2 diff). Telemetry tracking for inconsistencies. Entries still recorded (no data loss). **Code review fixes:** Triple-parsing eliminated (single `structured` variable reused across display/confidence/journal). JsonDocument leak fixed (`using var` on both `Parse()` calls). ConsoleTelemetryService `Console.WriteLine` → `Trace.WriteLine` (was bypassing trace listeners). **744/744 tests passing** (46 new). MacCatalyst build clean. **Next: Live test with Gemini brain + voice.**
>
> **Commits (Phase 16):** 5 plan execution commits, code review fix commit
> **Modified:** BrainPromptBuilder.cs, IBrainEventRouter.cs, BrainEventRouter.cs, MainViewModel.cs, OpenRouterModels.cs, BrainAnalysisResult.cs (new), OpenRouterClient.cs, OpenRouterBrainService.cs, MauiProgram.cs, IGameJournalService.cs, GameJournalService.cs, ConsoleTelemetryService.cs

> **2026-03-10 Phase 14 + Phase 15 Complete — Chess Context Engineering + Capture Pipeline Fix.** Two phases executed back-to-back. **Phase 14 (Chess Context Engineering, 6 plans):** Built production-quality context engineering for chess. ITelemetryService + ConsoleTelemetryService for pipeline observability. Removed dead Lichess fallback (Stockfish-only). GameJournalService for in-memory move-by-move journal with auto new-game detection via starting FEN. BrainPromptBuilder with system/user message split, connection-state awareness, L1 event injection (capped at 5). Brain vision prompt rewrite with identity, illusion language rules ("I can see the board"), tool awareness. In-game text chat routed to brain (bypasses voice WebSocket). **Phase 15 (Capture Pipeline Fix, 2 plans):** Fixed critical _activeTasks counter leak that permanently killed brain vision pipeline after session disconnect. Root cause: `Interlocked.Increment` before `Task.Run(delegate, cancelledToken)` — when token is pre-cancelled, runtime skips delegate, counter never decrements. Fix: increment INSIDE delegate, pass `CancellationToken.None` to `Task.Run`. Also replaced `IsBusy` boolean gate with `Channel(1, DropOldest)` frame slot pattern — `TrySubmitFrame` always succeeds, latest frame wins. Consumer loop (`ConsumeFramesAsync`) processes frames sequentially. Same fix applied to `SubmitQueryAsync`. MockBrainService updated to match production pattern. MainViewModel `FrameCaptured` handler rewired to `TrySubmitFrame`. Frame lifecycle telemetry: `frame_queued`, `frame_replaced`, `frame_dropped`, `frame_processing_start`, `frame_error`. **Code review fix:** Consumer loop now uses parameterless `ReadAllAsync()` (no CTS token) so it survives `CancelAll()` across session disconnects (service is DI singleton). MockBrainService Dispose now waits for consumer task (parity with production). **Known issues #1 (in-game chat) and #2 (brain not seeing captures) — BOTH FIXED.** Issue #3 (AI hallucinating) addressed by vision prompt guardrails. **698/698 tests passing** (50 new: 9 frame slot, 3 counter regression, 5 MockBrainService, 7 MainViewModel capture, 2 consumer-survives-CancelAll, plus 24 from Phase 14). **Next: Live test tomorrow, then deploy.**
>
> **Commits (Phase 14):** `3ecf1ae` research, `769f852` phase plan, then 6 plan execution commits per plan
> **Commits (Phase 15):** `ddbf354` RED tests, `8aa1a7b` GREEN implementation, `a19fcb8` RED tests (plan 2), `79e457d` GREEN implementation (plan 2), `30966bf` fix: consumer loop survives CancelAll

> **2026-03-09 MILESTONE: macOS Build Notarized and Ready for Distribution.** Repo moved from `~/Documents/5DOF Projects/gAImer/gAImer_desktop` to `~/Developer/gAImer_desktop` via fresh clone from GitHub — eliminates iCloud File Provider I/O timeouts, build time dropped from 90s to 4s. Native xcframeworks (GaimerGhostMode, GaimerScreenCapture) copied from old repo location (not in git). Release build published with `dotnet publish -c Release`, signed with Developer ID Application certificate (Ike Nlemadim, VW5K99T4JJ) with hardened runtime, submitted to Apple notarization service (submission c773e4ea), status: **Accepted**. Notarization ticket stapled to app bundle. Final distributable: `WitnessDesktop-notarized.zip` (39MB). Build script `scripts/build-release-mac.sh` automates the full pipeline (publish → sign → notarize → staple → zip). **Next:** Upload to Gaimer website for public download. Windows build pending (Ghost Mode Windows impl needed).

> **2026-03-07 MILESTONE: Brain Pipeline Verified End-to-End.** First successful full-pipeline live test session — ran stable for 10+ minutes against Apple Chess.app. **Pipeline verified:** Screen Capture (ScreenCaptureKit) → Brain (Claude Sonnet 4 via OpenRouter) → Timeline display → Voice narration (Gemini Live). Brain reads chess board from screenshots, delivers tactical advice in Leroy personality. Voice mode coexists with brain pipeline without conflict. Text chat (out-game) uses gpt-4o-mini worker model, personality-aware. **5 bugs fixed:** (1) Brain model ID: `anthropic/claude-sonnet-4-20250514` was wrong OpenRouter ID (no Anthropic dashboard activity) → fixed to `anthropic/claude-sonnet-4`. (2) MIME type: ImageProcessor outputs JPEG but `CreateImageAnalysisRequest` labeled `image/png` → now auto-detects from magic bytes (JPEG FF D8 vs PNG 89 50). (3) Ghost panel off-screen: `objc_msgSend_stret` returns garbage on arm64 (Apple Silicon doesn't use stret variant) → Swift-side auto-repositions panel to right edge of main screen on every `ghost_panel_show`. (4) Timestamps: "just now" computed property never updates → replaced with actual clock time (e.g., "1:19 am"). (5) Error surfacing: brain errors now show HTTP status + model name instead of generic "Check logs". **Observations:** Brain hallucinated some board positions (vision model limitation), each analysis is stateless (no move list tracking), brain doesn't know user's color (available in window title). OpenRouter audio models are REST-only — confirmed voice must stay on direct Gemini/OpenAI WebSocket APIs. **Architecture confirmed:** Voice = Gemini Live / OpenAI Realtime (direct WebSocket), Brain = OpenRouter REST (claude-sonnet-4 vision, gpt-4o-mini text/tools), Pipeline = Capture → ImageProcessor (50% scale, JPEG q60) → dHash diff gate → Brain → Channel<BrainResult> → BrainEventRouter → Timeline/Voice/Ghost. **519/519 tests passing, CI green.**
>
> **Commit:** `a8fc974` fix(live): brain model ID, JPEG MIME type, ghost panel positioning, timestamps
>
> **Modified:** `OpenRouterBrainService.cs` (model ID fix), `OpenRouterClient.cs` (MIME auto-detect from magic bytes), `GaimerGhostMode Swift source` (panel auto-reposition on show), `TimelineCheckpoint.cs` (clock time instead of relative), `BrainEventRouter.cs` (error message with HTTP status + model)

> **2026-03-06 Live Testing Sprint — .env Loading, Out-Game Chat, Error Display, Power Button.** First live testing session with real API keys. **.env File Loading:** Added `LoadDotEnv()` to MauiProgram.cs — walks up from AppContext.BaseDirectory + checks `~/.gaimer/.env` fallback for deployed apps (bundle can't access repo root). **Out-Game Text Chat:** Added `IBrainService.ChatAsync()` for request-reply text chat (not channel-based). OpenRouterBrainService builds personality-aware system prompt + last 10 chat history. Out-game chat path in MainViewModel now calls brain instead of returning canned mock reply. **UI Polish (4 fixes):** (1) Enter-to-send via TextChanged event detecting trailing `\n` (Shift+Enter handled natively by Mac Catalyst Editor), (2) newest-message-on-top via `ChatMessages.Insert(0, ...)` at all 6 call sites + `EventLines.Insert(0, ...)`, (3) fixed UTC time display → `ToLocalTime()`, (4) voice-without-connection DisplayAlert via `UnsupportedAudioFeatureToggled` event. **Error Display System:** Added `EventOutputType.SystemError` + `IBrainEventRouter.OnError()` — errors route to timeline as gray capsule pills (alert.png icon, `#30808080` bg — black/gray, white text, minimalistic). Brain channel errors now route through `OnError` instead of just `_topStrip`. `AddSystemMessage` also pushes to ghost card when ghost mode is active. **Power Button 3-State:** Gray (offline/mock) → Green (live API, tap opens connector) → Red (connected, tap disconnects). `IsLive` property checks `_brainService.ProviderName`. **519/519 tests passing (8 new).**
>
> **Modified:** `MauiProgram.cs` (.env loading), `IBrainService.cs` (ChatAsync), `OpenRouterBrainService.cs` (ChatAsync impl), `MockBrainService.cs` (ChatAsync mock), `MainViewModel.cs` (out-game chat, Insert(0), IsLive, AddSystemMessage routing), `MainPage.xaml` (power button 3-state, ChatEditor x:Name), `MainPage.xaml.cs` (Enter-to-send), `TimelineCheckpoint.cs` (ToLocalTime), `TimelineFeed.cs` (Insert(0)), `EventOutputType.cs` (SystemError), `EventIconMap.cs` (SystemError entries), `IBrainEventRouter.cs` (OnError), `BrainEventRouter.cs` (OnError impl, brain error routing), `MainViewModelTestBase.cs` (ProviderName default, ChatAsync mock), `MainViewModelTests.cs` (5 new tests), `BrainEventRouterTests.cs` (2 new/updated tests)

> **2026-03-06 Phase 08 Complete — Settings Bento Grid, Error Page, Audio Guards, App Icon.** Final Phase 08 polish sprint completing 5 UI enhancements. **Settings Bento Grid:** Rewrote SettingsPage.xaml from vertical stack to 4-card bento grid layout (Voice Config 2-col, Active Voice 1-col, System Info 2-col, About 1-col) with local ResourceDictionary, icon boxes, status badges, and tags. **Global Error Page:** New ErrorPage.xaml with dashed border container, warning icon avatar, error code badge, title/description/detail sections, "Return Home" CTA — registered in AppShell and DI. **Audio Feature Guards:** Added per-agent audio support flags (SupportsVoiceChat, SupportsVoiceCommand, SupportsGameAudio, SupportsAudioIn) to Agent model. MainViewModel toggle handlers check support flags and snap back unsupported toggles with UnsupportedAudioFeatureToggled event → MainPage DisplayAlert. **App Icon:** Custom 1024x1024 PNG icon added, csproj MauiIcon updated from SVG to PNG. Icon embedded correctly in .icns but Dock shows cached version (macOS icon cache behavior). **Capture Rate Unified:** Changed "1 FPS (diff-gated)" → "Every 30s + on every move" across Agent.cs, AgentSelectionPage, OnboardingPage, SettingsViewModel. **Onboarding Chevrons:** Hidden during Downloading/Ready states (ShowChevrons = State == AgentBrowse). **OrbitalLoader control created** (3 concentric spinning rings via MAUI Animation API) but not used in final UI — kept for future use. **Splash screen attempted** with GAIMER logo but reverted to white ActivityIndicator due to image weight causing lag. **Phase 08 marked Complete (5/5 plans, 100%).** 511/511 tests passing.
>
> **Modified:** `Views/SettingsPage.xaml` (bento grid rewrite), `Views/ErrorPage.xaml` + `.cs` (new), `Controls/OrbitalLoader.cs` (new), `Models/Agent.cs` (audio flags, capture rate), `ViewModels/MainViewModel.cs` (audio guards, suppress flag, event), `ViewModels/OnboardingViewModel.cs` (ShowChevrons), `ViewModels/SettingsViewModel.cs` (capture rate), `Views/AgentSelectionPage.xaml` (capture rate), `Views/OnboardingPage.xaml.cs` (capture rate), `MainPage.xaml` (white spinner), `MainPage.xaml.cs` (UnsupportedAudioFeatureToggled subscription), `AppShell.xaml.cs` (Error route), `MauiProgram.cs` (ErrorPage DI), `WitnessDesktop.csproj` (MauiIcon PNG), `Resources/AppIcon/appicon.png` (new), `.planning/STATE.md`, `CLAUDE.md`

> **2026-03-06 CI Workflow + Documentation Sync.** GitHub Actions CI workflow pushed to develop and confirmed green. Fixed 3 CI issues: (1) `dotnet restore` fails for MAUI multi-target projects on ubuntu — removed separate restore step, used `dotnet build -f net8.0 -p:TargetFrameworks=net8.0` to override project graph, (2) SkiaSharp needs `SkiaSharp.NativeAssets.Linux.NoDependencies` in test csproj for `libSkiaSharp.so`, (3) `libfontconfig1` apt dependency for SkiaSharp on Linux. README updated (CI badge, 511 tests, phases 01-11, agent table). CHANGELOG updated (v0.8.0-v0.11.0 entries). Owner instruction for CI marked complete. Notarization prep owner instruction created. Documentation audit performed across all project docs, CLAUDE.md, MEMORY.md, Agentic Office — all synced to current state. **511/511 tests passing.**
>
> **Modified:** `.github/workflows/dotnet-test.yml`, `WitnessDesktop.Tests.csproj` (SkiaSharp Linux assets), `README.md`, `CHANGELOG.md`, `.planning/ROADMAP.md`, `.planning/STATE.md`, `CLAUDE.md`

> **2026-03-06 Stockfish Path Fix + Regression Tests.** Fixed Stockfish download path — `SpecialFolder.ApplicationData` resolves to `~/Documents/.config/` on Mac Catalyst (File Provider blocks process execution). Fix: use `FileSystem.AppDataDirectory` which resolves to proper app container path. 7 UI polish gap tests + regression tests for Stockfish path fix. **511/511 tests passing (9 new).**
>
> **Commits:** `efc3751` (Stockfish path fix), `6266856` (7 UI polish tests), `bf18064` (Stockfish regression tests)

> **2026-03-06 UI Polish Sprint — Audio Panel, Ghost FAB, Timeline, Chat Input.** Comprehensive UI polish across multiple surfaces. **Audio Panel Overhaul:** Renamed and reordered 4 toggles: VOICE CHAT, VOICE COMMAND, GAME AUDIO, AUDIO IN. Added `IsAudioInActive` property to MainViewModel. AUDIO IN uses rose LED color (#e11d48). All labels uppercase. **Ghost FAB:** Shows agent portrait when connected (replaces illegible "TAP FOR GHOST MODE" label). Reduced size by 10pt (120→110 outer, 110→100 inner). Ghost hint system: appends "Tap {AgentName} for Ghost Mode" to live messages until user taps ghost button for the first time — persisted via Preferences with `#if` compilation guard. **User Profile:** Reduced size 120→110 / 110→100 to match ghost button. **Power Button:** Opens game selector when disconnected (second connect path via ShowWindowPickerCommand), keeps power icon (not profile pic). **Toggle Orientation Fixed:** IndustrialToggleSwitch AnimateToState and pan snap logic corrected so up=ON, down=OFF (was inverted). **Agent Labels Unified:** Wasp "Chess Mistress" → "Chess Master" across Agent.cs, AgentSelectionPage badge, OnboardingPage connect/ready stages. **Timeline Polish:** Replaced [in-game]/[out-game] with relative timestamps (just now, Xs ago, Xm ago, Xh ago, Xd ago). Added seconds tier (5-59s) to prevent duplicate "just now" labels. Reduced all timeline text ~20% (headers 27→22, badges 23→18, events 24→19). **Chat Input Reduction:** Editor font 30→24, send button 54→44, padding/margins reduced ~20%. **DirectMessageBubble/ProactiveAlertView:** Text reduced ~20% across all label sizes. **Phase 12 Documented:** Audio Intelligence Pipeline added to ROADMAP (Voice Command/Whisper, Game Audio/SCK, Audio In/virtual mic). **502/502 tests passing.**
>
> **Modified:** `MainPage.xaml` (profile circle, power button, chat input, FAB overlay sizing), `MainViewModel.cs` (IsAudioInActive, ShowGhostHint, ghost hint persistence), `AudioControlPanelView.xaml` (4 toggles, reordered, all caps), `FabOverlayView.xaml` (portrait on connect, reduced size), `IndustrialToggleSwitch.xaml` + `.cs` (up=ON orientation fix), `Agent.cs` (Wasp Id → Chess Master), `AgentSelectionPage.xaml` (Chess Master badge), `OnboardingPage.xaml.cs` (simplified connect/ready labels), `TimelineCheckpoint.cs` (seconds tier in ContextBadge), `TimelineView.xaml` (reduced text), `DirectMessageBubble.xaml` (reduced text), `ProactiveAlertView.xaml` (reduced text), `.planning/ROADMAP.md` (Phase 12), `.planning/STATE.md` (Phase 12 row)

> **2026-03-05 MainPage V4 Layout Balance + Polish.** Fixed main chat interface vertical centering imbalance. **Root cause:** outer grid had nested row grids where Row 0 height was driven by the capture preview (~210px) in Column 0, but the live message (~110px) in Column 1 used `VerticalOptions="Start"`, leaving ~100px dead space below it — making the chat card appear pushed toward the bottom bar. **Fix:** Flattened outer grid to `ColumnDefinitions="280,*,Auto"` with the left sidebar (capture preview + connectors) using `Grid.RowSpan="2"` to span Row 0 + Row 1. Row 0 height now driven by live message/buttons (~122px) instead of capture preview, giving the card ~88px more vertical space and balanced gaps above/below. Left sidebar visual position unchanged. **Audio panel cleanup:** Removed Border container (background + stroke) from AudioControlPanelView — toggles now float cleanly without compressed appearance. **Chat card breathing room:** Added `Margin="10"` to main chat card Border for visual spacing from surrounding elements. All changes are layout-only — no logic, no test impact. **502/502 tests passing.**
>
> **Modified:** `MainPage.xaml` (grid restructure — flat columns, sidebar RowSpan, card margin), `Views/AudioControlPanelView.xaml` (removed Border wrapper)

> **2026-03-05 MainPage V3 Layout Redesign + Connect Animation + Ghost 3-State Model.** Comprehensive MainPage layout restructure and visual polish sprint. **Layout Restructure:** Removed agent profile picture from top bar entirely. Moved capture preview from sidebar to top bar (280px left column). Live message window now center column with fixed HeightRequest=110 (3-4 lines, vertically centered, independent of preview height). Sidebar simplified to connectors-only — full vertical space freed for future game connectors (RPG, FPS). **Bottom Bar Enhanced:** Agent name + specialization + inverted VAD (FlowDirection=RightToLeft, speaker icon ScaleX=-1 to face VAD direction) positioned left of ghost button as compact status cluster. **Ghost Button 3-State Model:** Added `IsGhostActive` computed property (IsConnected && IsFabActive) to MainViewModel with change notifications on both IsFabActive and ConnectionState. FabOverlayView now has 3 visual states: Disconnected (muted gray ring, black "GHOST MODE", not tappable), Connected (green glow ring, white "TAP FOR GHOST MODE" via MultiTrigger, tappable), Ghost Active (green glow ring, agent portrait, tappable). **Gun Metal Color Purge:** Eliminated all violet/purple bias across 7+ files — MainPage, AudioControlPanelView (#1a1225→#12151a), IndustrialToggleSwitch (#1a1225→#1a1d21), HorizontalIndustrialToggle, FabOverlayView (idle ring yellow→muted #3a3d42). **Circular Button Design Language:** Disconnect button (power icon ⏻, red glow when connected), settings button (hamburger ☰), user profile (metallic gradient 120px circle with username from sign-in), ghost mode — all share metallic gradient (#8a8a98→#3a3a48). All dividers removed (1 vertical, 2 horizontal), grid simplified 5→3 rows. **Connect Button Flip Animation:** OnboardingPage Ready state — ScaleYTo(0) → swap bg to dark #1a1d21, show "CONNECTING..." label → ScaleYTo(1) → execute ConnectCommand (1.2s delay for animation). **Other Fixes:** Agent header uses PortraitImage (profile pic) instead of IconImage (card art). Chat placeholder dynamic ("Ask Leroy..." per agent). Plus pattern PlusSize 90→50 for denser infinite-space illusion. ConnectAsync removed MainThread.InvokeOnMainThreadAsync wrapper (relay commands already on main thread — reduces post-onboarding freeze). Connector label simplified "Any Chess Game"→"Chess". Info icon removed from connector. User profile circle with username bound to SelectedAgent.UserId with Guest fallback.
>
> **Modified:** `MainPage.xaml` (major restructure — top bar, sidebar, bottom bar, colors, buttons, dividers), `MainPage.xaml.cs`, `MainViewModel.cs` (IsGhostActive, ChatInputPlaceholder), `OnboardingPage.xaml` (Connect button flip structure), `OnboardingPage.xaml.cs` (flip animation), `OnboardingViewModel.cs` (IsConnecting, ConnectAsync navigation fix), `FabOverlayView.xaml` (3-state model), `AudioControlPanelView.xaml` (gun metal), `IndustrialToggleSwitch.xaml` (gun metal), `HorizontalIndustrialToggle.xaml` (gun metal)

> **2026-03-05 Onboarding Flow — Login/Invite + Agent Presentation UI.** New `OnboardingPage` with 4-state flow: SignIn → AgentBrowse → Downloading → Ready → MainPage. Two-panel card layout (960×560) with gun metal / metallic black design aesthetic. Left panel: Gaimer logo (SignIn) or agent portrait (Browse/Download/Ready). Right panel: sign-in form, agent details with colored tool tags, download progress, connect button. **New files:** `OnboardingPage.xaml`, `OnboardingPage.xaml.cs`, `OnboardingViewModel.cs`. **Auth changes:** Added `SignInWithEmailAsync(email, username)` to `IAuthService`; implemented in `SupabaseAuthService` (with dev auto-approve fallback when SUPABASE_URL not configured) and `MockAuthService`. **Custom font:** Installed Krophed.otf for "Got an invite?", agent names, "INSTALLING" headers. **Entry fix:** Added `EntryHandler.Mapper` to strip native Entry chrome (BorderStyle=None on Mac Catalyst). **"Select a Gaimer" header** centered at top of card during agent browse. **Bug fix:** Engine start failure (`_stockfishService.StartAsync()`) was throwing in catch block, sending state back to AgentBrowse instead of Ready — wrapped in isolated try-catch so download→ready transition always succeeds. **Logo 30% larger** (HeightRequest 160→208). **Metallic highlights:** Brightened field labels, input borders, subtitle text for light-reflection effect on gun metal surface. Shell startup route changed from AgentSelectionPage to OnboardingPage. DI registered OnboardingViewModel + OnboardingPage. **502/502 tests passing, zero regressions.**
>
> **New Files:** `Views/OnboardingPage.xaml`, `Views/OnboardingPage.xaml.cs`, `ViewModels/OnboardingViewModel.cs`, `Resources/Fonts/Krophed.otf`
>
> **Modified:** `IAuthService.cs` (SignInWithEmailAsync), `MockAuthService.cs`, `SupabaseAuthService.cs` (dev fallback + production validate-invite), `AppShell.xaml` (startup route), `AppShell.xaml.cs` (route registration), `MauiProgram.cs` (DI + Krophed font), `App.xaml.cs` (removed auth gating)

> **2026-03-05 Phase 08 Bug Fix Sprint — Live Testing & Critical Fixes.** Four bugs discovered and fixed during live testing session. **Bug 1: Catalyst Window Invisible** — Cleaned App.xaml.cs from ~460 to ~85 lines by removing all dead diagnostic code (CGS private API, CGWindowList P/Invoke, RequestGeometryUpdate, ObjC runtime interop, file-based logging, screen detection heuristics). This was the final piece of the window visibility fix (previous session added SceneDelegate, AppDelegate overrides, Program.cs state clearing). Window now loads and renders correctly. **Bug 2: MainPage Blackout After Agent Selection** — `IsPageReady` property on MainViewModel was never set to `true`. MAUI binding timing issue: when `BindingContext` is set, bindings evaluate immediately with current property values. Fix: set `_viewModel.IsPageReady = true` and `_viewModel.SelectedAgent` BEFORE `BindingContext = _viewModel` in `OnNavigatedTo`. **Bug 3: Chess Connector Toggle Unresponsive** — Toggle was bound to `IsVoiceChatActive` (wrong binding), which triggered voice chat check requiring connection, snapping back to OFF. Fixed binding to `IsConnected` (OneWay), set `IsInteractive="False"` + `InputTransparent="True"` on Switch (child gesture recognizers consume touches before parent), added `TapGestureRecognizer` on Border for `ShowWindowPickerCommand`. **Bug 4 (cosmetic): Page Transition Flash** — Added loading spinner overlay (purple ActivityIndicator on dark background while `IsPageReady` is false). **Ghost Panel Positioning (WIP):** Added `SetPosition(x, y)` to IGhostModeService interface with native `ghost_panel_set_position` P/Invoke. Added NSScreen enumeration via ObjC runtime interop in MainViewModel. Positioning logic didn't produce correct results — deferred to making panel freely draggable. **Stockfish download bug discovered:** `SpecialFolder.ApplicationData` resolves to `~/Documents/.config/` on Mac Catalyst (not `~/Library/Application Support/`), blocking process execution. **502/502 tests passing (14 new).**
>
> **Three commits:** `681853f` (Catalyst window + MainPage blackout + Chess connector), `3b7ae42` (loading spinner), `8c389a3` (ghost panel SetPosition WIP)
>
> **Modified:** `App.xaml.cs` (complete cleanup), `MainPage.xaml` (connector fix, loading spinner), `MainPage.xaml.cs` (IsPageReady + BindingContext ordering), `MainViewModel.cs` (IsPageReady, PositionGhostPanelOnTargetScreen), `IGhostModeService.cs` (SetPosition), `MacGhostModeService.cs` (SetPosition impl), `MockGhostModeService.cs` (SetPosition no-op), `SceneDelegate.cs` (logging cleanup)

> **2026-03-04 Phase 11 Execution + Phase 08 Polish Fixes.** Executed all 6 Phase 11 plans (Agent Personality System) across 3 waves plus applied 5 Phase 08 code review fixes. **Wave 1 (Plans 01-03, Code Infrastructure):** Restructured Agent.cs with 5 personality blocks (SoulBlock, StyleBlock, BehaviorBlock, SituationsBlock, AntiPatternsBlock) + ToolGuidanceBlock + BrainPersonalityPrefix. ComposedPersonality computed property composes all blocks with section headers ([WHO YOU ARE], [HOW YOU TALK], [HOW YOU BEHAVE], [SITUATIONAL MODES], [NEVER DO]) and appends ToolGuidanceBlock. Updated IChatPromptBuilder/ChatPromptBuilder with Agent? parameter — text chat uses SOUL + BEHAVIOR only, falls back to Dross CoreIdentity. Added AgentKey to SessionContext; OpenRouterBrainService reads AgentKey → Agents.GetByKey → BrainPersonalityPrefix for brain prompts. **Wave 2 (Plans 04-05, Personality Content):** Composed Leroy personality from 64-question builder questionnaire — cocky knight-obsessed tactical wildcard ("Respect the knight"). Composed Wasp as distinct contrast — composed queen archetype, positional, measured ("Control the board, control the game"). Both as compile-time const strings. Created design files in `WITNESS/gaimer_spec_docs/agents/leroy/` and `agents/wasp/` (SOUL, STYLE, BEHAVIOR, SITUATIONS, ANTI-PATTERNS, EXAMPLES). **Wave 3 (Plan 06, Integration):** Voice providers (GeminiLiveService, OpenAIRealtimeService) now use ComposedPersonality instead of SystemInstruction. **Phase 08 Polish Fixes:** (1) SupabaseAuthService Console.WriteLine → Debug.WriteLine + IDisposable + username redaction, (2) SettingsService cached fallback device ID, (3) OpenAIRealtimeService removed APIKEY/API_KEY cross-contamination fallback, (4) MainViewModel Shell.Current null-guards at 6 points, (5) ComposedPersonality cached with ??= pattern. **Code review:** 0 critical, 2 medium (both fixed), 9 low, 4 nitpick findings. 30 new personality tests. **488/488 total tests passing (30 new), zero regressions.**
>
> **New Files:** `Tests/Personality/AgentPersonalityTests.cs`, `WITNESS/gaimer_spec_docs/agents/leroy/{SOUL,STYLE,BEHAVIOR,SITUATIONS,ANTI-PATTERNS,EXAMPLES}.md`, `WITNESS/gaimer_spec_docs/agents/wasp/{SOUL,STYLE,BEHAVIOR,SITUATIONS,ANTI-PATTERNS,EXAMPLES}.md`
>
> **Modified:** `Models/Agent.cs` (complete rewrite with personality architecture), `Models/SessionContext.cs` (AgentKey), `Services/IChatPromptBuilder.cs` (Agent? param), `Services/ChatPromptBuilder.cs` (agent-aware identity), `Services/Brain/OpenRouterBrainService.cs` (GetBrainPersonality), `Services/GeminiLiveService.cs` (ComposedPersonality), `Services/OpenAIRealtimeService.cs` (ComposedPersonality + removed APIKEY fallback), `Services/Auth/SupabaseAuthService.cs` (Debug.WriteLine + IDisposable), `Services/SettingsService.cs` (cached fallback), `ViewModels/MainViewModel.cs` (AgentKey + Shell.Current guards)

> **2026-03-03 Phase 11 Planning: Agent Personality System.** Comprehensive agent personality gap analysis completed. Identified 4 gaps: (1) personality absent from brain analytical outputs — brain uses generic "gaming AI analyst" prompt, (2) no DIM-like intent classification framework, (3) no multi-LLM persona consistency — brain and voice use different personality sources, (4) "what to say" vs "how to say it" not separated. Researched OpenClaw agent architecture and aaronjmars/soul.md framework — sourced 9 reference files into `WITNESS/gaimer_spec_docs/agent-personality-reference/`. Key insight: "behavior is defined through specificity and situations, not adjectives." Mapped OpenClaw patterns to gAImer: SOUL.md→agent personality core, STYLE.md→voice character, AGENTS.md→operating contract, SKILL.md→situational playbooks. Designed integration architecture: separate design files for human authoring, composed into const strings in Agent.cs at compile time (same pattern as ChessToolGuidance). Created 6 plan files across 3 waves: Wave 1 (Plans 01-03) = code infrastructure (Agent.cs restructure, ChatPromptBuilder agent-awareness, OpenRouterBrainService personality injection); Wave 2 (Plans 04-05) = personality content composition (blocked on owner completing builder questionnaire); Wave 3 (Plan 06) = integration verification. Created 64-question personality builder questionnaire in Agentic Office for Leroy and Wasp, covering SOUL (identity, worldview, tensions), STYLE (voice character, vocabulary, reactions), BEHAVIOR (priorities, teaching style, proactive/reactive triggers), SITUATIONS (opening, critical moments, winning/losing, teaching mode), ANTI-PATTERNS (never-say list, wrong voice examples), and CALIBRATION (good/bad output examples with same scenarios for distinctiveness comparison). Token budget: voice ~1300 tokens, brain ~200 tokens, well within limits.
>
> **New Files:** `.planning/phases/11-agent-personality-system/11-01-PLAN.md` through `11-06-PLAN.md`, `Agentic Office/RemoteAgents/Gaimer-Desktop-Brain/agent-personality-builder-questionnaire.md`
>
> **Modified:** `.planning/ROADMAP.md`, `.planning/STATE.md`

> **2026-03-03 Phase 10: Integration & Orchestration Test Coverage (TDD, 4 Plans).** Closed end-to-end verification gaps identified by system audit. TDD approach across 4 sequential plans. **Plan 10-01 (Foundation):** Fixed TestStubs.cs (MainThread, ImageSource, Shell, Application stubs for net8.0). Added VoiceConfigTests (7), SettingsServiceTests (10), MockAuthServiceTests (4), MockConversationProviderTests (10). 31 new tests, 332/332 total. **Plan 10-02 (Provider Factory + Auth):** ConversationProviderFactoryTests (15 — provider selection, auto-detect, missing keys, voice gender). Refactored SupabaseAuthService with internal constructor for HttpMessageHandler injection. SupabaseAuthServiceTests (12 — device validation, API key fetching, error paths). 27 new tests, 359/359 total. **Plan 10-03 (MainViewModel Orchestration):** Enabled MainViewModel for net8.0 compilation (selective csproj re-include + MAUI type stubs). MainViewModelTestBase (12-mock factory). MainViewModelTests (53 — constructor/lifecycle/toggle/stop/send/frame/connection/text events). 53 new tests, 412/412 total. **Plan 10-04 (AgentSelection + Voice Guards + Integration):** Enabled AgentSelectionViewModel for net8.0. AgentSelectionViewModelTests (19 — init, download, navigation, Stockfish lifecycle). VoiceServiceGuardTests (21 — null agent, empty API key, disconnected state guards for Gemini + OpenAI). PipelineIntegrationTests (6 — FrameDiff→Brain→Channel→Router E2E, session/tool consistency, context flow, provider lifecycle, Stockfish lifecycle). 46 new tests, 458/458 total. **Code review findings:** 3 high (HttpClient leak in SupabaseAuthService, device ID logged, OpenAI APIKEY fallback cross-contamination), 5 medium (NRE-catching disconnect tests, Disconnecting state inconsistency, Task.Delay sync, unstable fallback GUID, fragile mock pattern). All deferred to Phase 08 (Polish). **458/458 total tests passing (157 new in Phase 10), zero regressions.**
>
> **New Files:** `Tests/Models/VoiceConfigTests.cs`, `Tests/Services/SettingsServiceTests.cs`, `Tests/Services/Auth/MockAuthServiceTests.cs`, `Tests/Services/Auth/SupabaseAuthServiceTests.cs`, `Tests/Conversation/ConversationProviderFactoryTests.cs`, `Tests/Conversation/MockConversationProviderTests.cs`, `Tests/Conversation/VoiceServiceGuardTests.cs`, `Tests/ViewModels/MainViewModelTestBase.cs`, `Tests/ViewModels/MainViewModelTests.cs`, `Tests/ViewModels/AgentSelectionViewModelTests.cs`, `Tests/Integration/PipelineIntegrationTests.cs`
>
> **Modified:** `TestStubs.cs`, `WitnessDesktop.csproj`, `SupabaseAuthService.cs` (internal ctor)

> **2026-03-03 Phase 09 Plans 02-04: Dual Tools, Agent Prompts, Integration (TDD).** Completed remaining Phase 09 plans in 3 waves. **Plan 02 (Wave 1):** Created `analyze_position_engine` + `analyze_position_strategic` tool definitions in `ToolDefinition.cs`. Updated `SessionManager` InGame tools (7 total: 3 always + 4 InGame). Rewrote `ToolExecutor` with IStockfishService injection, FEN validation gate, Stockfish-first/Lichess-fallback for engine analysis, LLM-based strategic analysis via OpenRouter. Simplified `GetAvailableToolDefinitions` to use `ParametersSchema` directly. DI registration updated in `MauiProgram.cs`. 35 new tests, 240/240 total. **Plan 03 (Wave 2):** Added `ChessToolGuidance` constant (dual-tool guidance, FEN extraction rules, voice output conventions) appended to Leroy and Wasp SystemInstruction. Updated Tools lists to `["capture_screen", "analyze_position_engine", "analyze_position_strategic", "get_game_state", "web_search"]`. Built "Chess Skills" download overlay in `AgentSelectionPage.xaml` (DOWNLOAD/SKIP buttons, progress bar, error display). Injected `IStockfishService` into `AgentSelectionViewModel` (download flow) and `MainViewModel` (StopAsync on disconnect). 26 new tests, 266/266 total. **Plan 04 (Wave 3):** 7 end-to-end tests (ChessToolEndToEndTests: full pipeline with MockStockfish). 6 live engine tests (StockfishLiveTests: real Stockfish 18 — UCI handshake, opening/midgame analysis, multi-PV, mate detection, cancellation). Fixed `MinHeightRequest` XAML error for MacCatalyst. Both net8.0 and maccatalyst builds clean. **279/279 total tests passing (127 new in Phase 09), zero regressions.**
>
> **New Files:** `Tests/Chess/ToolExecutorChessTests.cs`, `Tests/Chess/AgentChessTests.cs`, `Tests/Chess/StockfishLifecycleTests.cs`, `Tests/Chess/ChessToolEndToEndTests.cs`, `Tests/Chess/StockfishLiveTests.cs`
>
> **Modified:** `Models/Agent.cs`, `Models/ToolDefinition.cs`, `Services/SessionManager.cs`, `Services/Brain/ToolExecutor.cs`, `ViewModels/AgentSelectionViewModel.cs`, `ViewModels/MainViewModel.cs`, `Views/AgentSelectionPage.xaml`, `Views/AgentSelectionPage.xaml.cs`, `MauiProgram.cs`

> **2026-03-03 Phase 09 Plan 01: Stockfish Engine Service (TDD).** Executed Plan 09-01 with strict TDD discipline (RED -> GREEN for each component). Created `Services/Chess/` directory with 6 production files: `IStockfishService.cs` (interface + AnalysisOptions/EngineAnalysis/EngineVariation records), `FenValidator.cs` (static FEN validation — ranks, pieces, king count, pawn placement, side to move, castling), `UciParser.cs` (stateless UCI protocol parser — info depth lines with cp/mate/multipv, bestmove with ponder, nodes/time), `StockfishService.cs` (child process management via System.Diagnostics.Process, UCI handshake, async analysis with SemaphoreSlim serialization, Threads=2/Hash=128 config), `StockfishDownloader.cs` (platform-aware download for macOS ARM64/x64 + Windows x64, SHA256 verification, atomic rename, chmod +x), `MockStockfishService.cs` (canned results for 3 positions: starting, Sicilian, Scholar's mate; fallback +30cp/e2e4). Test files: `Chess/FenValidatorTests.cs` (20 tests), `Chess/UciParsingTests.cs` (14 tests), `Chess/StockfishServiceTests.cs` (19 tests — mock lifecycle, canned results, FEN validation, cancellation, model records, downloader). Total: 193/193 tests passing (53 new + 140 existing), zero regressions.
>
> **New Files:** `Services/Chess/IStockfishService.cs`, `Services/Chess/FenValidator.cs`, `Services/Chess/UciParser.cs`, `Services/Chess/StockfishService.cs`, `Services/Chess/StockfishDownloader.cs`, `Services/Chess/MockStockfishService.cs`, `Tests/Chess/FenValidatorTests.cs`, `Tests/Chess/UciParsingTests.cs`, `Tests/Chess/StockfishServiceTests.cs`

> **2026-03-03 Showcase Agent Initialization & TDD Mandate.** Created showcase-ready project files: README.md (project overview, architecture diagram, build/test/run instructions, tech stack, roadmap), CHANGELOG.md (v0.1.0-alpha through v0.6.0-alpha release history), .github/workflows/dotnet-test.yml (CI workflow — dotnet test on push/PR to main/develop, net8.0 target, LiveApi filter exclusion). Updated .gitignore with AI tool directories (.claude/, .codex/, .opencode/, .agents/, .axon/). Installed TDD skill (obra/superpowers@test-driven-development) and updated gaimer-desktop-engineer and gaimer-code-reviewer agent CLAUDE.md files with TDD-first development mandate. CI workflow file could not be pushed to GitHub — PAT requires `workflow` scope; owner instruction created in Agentic Office RemoteAgents docs. All 140/140 tests passing. Pushed to develop (commit b783bfd).
>
> **New Files:** `README.md`, `CHANGELOG.md`, `.github/workflows/dotnet-test.yml`
>
> **Modified:** `.gitignore`, `.claude/agents/gaimer-desktop-engineer/CLAUDE.md`, `.claude/agents/gaimer-code-reviewer/CLAUDE.md`

> **2026-03-03 Phase 3+4 Tests — 27 Tests Complete, Full Coverage Achieved (140/140).** Phase 3 Trivial Path (21 tests, 5 files): AudioResamplerTests (8 — resample fast path, up/downsample byte counts, stereo-to-mono averaging, float32-to-int16 clamping, null input guard), AudioFormatTests (3 — input/output byte durations, linear scaling), ContentConverterTests (5 — polymorphic JSON read/write for OpenRouterMessage.Content: string, List<ContentPart>, null), OpenRouterSerializationTests (3 — round-trip with snake_case naming, null omission via WhenWritingNull), BrainResultTests (2 — default Priority=WhenIdle, default CreatedAt=UtcNow). Phase 4 Integration (6 tests, 3 files): ChannelPipelineTests (2 — multi-producer Channel<BrainResult> consumer reads all, BrainEventRouter channel consumer routes to timeline), EndToEndTests (2 — BrainContextService full envelope ingest→build→format, SessionManager OutGame→InGame→OutGame tool cycle), LiveApiTests (2 — Lichess cloud eval real HTTP, OpenRouter chat completion real API, both env-var gated for offline CI).
>
> **New Files:** `Audio/AudioResamplerTests.cs`, `Audio/AudioFormatTests.cs`, `Models/ContentConverterTests.cs`, `Models/OpenRouterSerializationTests.cs`, `Models/BrainResultTests.cs`, `Integration/ChannelPipelineTests.cs`, `Integration/EndToEndTests.cs`, `Integration/LiveApiTests.cs`

> **2026-03-03 Phase 2 Easy Path Tests — 29 Unit Tests + ToolDefinition Schema Fix.** Implemented all 29 Phase 2 unit tests across 5 files: SessionManagerTests (8 — state transitions, event firing, tool gating), ToolDefinitionTests (3 — JSON Schema validation, RequiresInGame correctness), TimelineFeedTests (8 — checkpoint creation/prepend ordering, event grouping by OutputType, auto-checkpoint from session state), EventIconMapTests (3 — icon/color/stroke coverage for all 11 generic EventOutputTypes), ChatPromptBuilderTests (7 — core identity, behavior rules per session state, tool listing). Fixed implementation gap: ToolDefinitions.ParametersSchema was defaulting to "{}" on all 6 tools — populated with proper JSON Schema per brain-and-tools-reference spec (web_search: query required, player_history/analytics: username required, in-game tools: empty properties for parameterless cache reads). Code review found 1 medium issue (JsonDocument disposal in tests) — fixed. Total: 113/113 test cases passing.
>
> **New Files:** `WitnessDesktop.Tests/Session/SessionManagerTests.cs`, `WitnessDesktop.Tests/Session/ToolDefinitionTests.cs`, `WitnessDesktop.Tests/Timeline/TimelineFeedTests.cs`, `WitnessDesktop.Tests/Timeline/EventIconMapTests.cs`, `WitnessDesktop.Tests/Prompts/ChatPromptBuilderTests.cs`
>
> **Modified:** `Models/ToolDefinition.cs` (ParametersSchema populated on all 6 tools)

> **2026-03-03 Phase 1 Critical Path Tests — 82 Unit Tests Implemented & Passing.** Implemented all 82 Phase 1 critical path unit tests across 10 files (3 helpers + 7 test classes). Created shared test helpers: TestImageFactory (SkiaSharp gradient/checkerboard PNG generation), MockHttpHandler (intercepts HTTP at handler level for OpenRouterClient/Lichess testing), ReflectionHelper (private static method invocation). Wave 1: ToolExecutorTests (20 tests — tool dispatch, ClassifyEvaluation ranges, mate detection, error handling, tool gating Theory), FrameDiffServiceTests (10 tests — dHash determinism, Hamming distance, debounce window, FrameChanged event), ImageProcessorTests (4 tests — scale/compress/thumbnail). Wave 2: BrainEventRouterTests (20 tests — signal→EventOutputType mapping, voice forwarding, timeline events, TopStrip callbacks), OpenRouterBrainServiceTests (6 tests — TruncateForVoice via reflection, CancelAll, IsBusy). Wave 3: BrainContextServiceTests (15 tests — token budgets, L1/L2 filtering, envelope confidence, budget allocation, format sections), VisualReelServiceTests (7 tests — append/ordering/trim policies).
>
> **Key fix:** Solid-color PNGs produce dHash=0 (all adjacent pixels equal) — switched to gradient/checkerboard test images.
>
> **Enabled `dotnet test` from CLI** by adding `net8.0` as a library-only target to both main project and test project. MacCatalyst uses Mono runtime; `dotnet test` CLI uses CoreCLR — fundamentally incompatible. Solution: dual-target with conditional MAUI exclusion. Created TestStubs.cs (ImageSource, MainThread stubs + global usings for net8.0 only). Excluded Views, Controls, ViewModels, Utilities, platform code from net8.0 build. Upgraded SkiaSharp from 2.88.9→3.119.1 for net8.0 compatibility. Result: `dotnet test -f net8.0` runs all 84 test cases (82 Facts + 1 Theory×2 InlineData) in ~4 seconds from any terminal. No IDE required.
>
> **New Files:** `WitnessDesktop.Tests/Helpers/TestImageFactory.cs`, `WitnessDesktop.Tests/Helpers/MockHttpHandler.cs`, `WitnessDesktop.Tests/Helpers/ReflectionHelper.cs`, `WitnessDesktop.Tests/Brain/ToolExecutorTests.cs`, `WitnessDesktop.Tests/Brain/BrainEventRouterTests.cs`, `WitnessDesktop.Tests/Brain/OpenRouterBrainServiceTests.cs`, `WitnessDesktop.Tests/FrameAnalysis/FrameDiffServiceTests.cs`, `WitnessDesktop.Tests/FrameAnalysis/ImageProcessorTests.cs`, `WitnessDesktop.Tests/Context/BrainContextServiceTests.cs`, `WitnessDesktop.Tests/Context/VisualReelServiceTests.cs`, `WitnessDesktop/TestStubs.cs`
>
> **Modified:** `WitnessDesktop/WitnessDesktop.csproj` (net8.0 multi-target), `WitnessDesktop.Tests/WitnessDesktop.Tests.csproj` (net8.0 target)

> **2026-03-02 Phase 06: ToolExecutor Live Wiring.** Replaced stub implementations with real service integrations. get_best_move now calls Lichess cloud eval API — parses FEN from args, queries `api/cloud-eval`, classifies centipawn evaluation across 7 ranges (>500 Winning, >200 Clear advantage, >100 Slight advantage, >-100 Equal, >-200 Slight disadvantage, >-500 Clear disadvantage, else Losing), handles forced mate detection, graceful error fallback on network failure. web_search now queries OpenRouter worker model (gpt-4o-mini) as a gaming knowledge assistant with 512-token limit. player_history/player_analytics return Phase 07 "not_available" stubs. DI wiring in MauiProgram.cs creates Lichess HttpClient (5s timeout, JSON accept header) and passes OpenRouterClient + worker model to ToolExecutor constructor. Removed unused HighlightRegion and PlayMusic tool definitions from ToolDefinitions and SessionManager.
>
> **Modified:** `MauiProgram.cs`, `Services/Brain/ToolExecutor.cs`, `Models/ToolDefinition.cs`, `Services/SessionManager.cs`

> **2026-03-02 Test Infrastructure & Coverage Report.** Established production-grade test infrastructure with xUnit 2.9 + Moq 4.20 + FluentAssertions 6.12 targeting net8.0-maccatalyst. Created comprehensive test coverage report (WITNESS/TEST_COVERAGE_REPORT.md) cataloging 138 test cases across 8 domains: Brain Pipeline (46 tests), Context & Memory (22), Session & State (11), Timeline (11), Prompt Building (7), Frame Analysis (14), Audio Utilities (11), Models & Serialization (10), Integration (6). MacCatalyst test project required significant MSBuild workarounds: OutputType=Exe for Mono/ILLink compatibility, Directory.Build.targets to override _MonoReadAvailableComponentsManifest, MtouchLink=None for trimming. Build succeeds; dotnet test CLI requires VS/Rider test explorer (Mono vs CoreCLR limitation). Configured OpenRouter API key in .env.
>
> **New Files:** `WitnessDesktop.Tests/WitnessDesktop.Tests.csproj`, `WitnessDesktop.Tests/GlobalUsings.cs`, `WitnessDesktop.Tests/Directory.Build.targets`, `WITNESS/TEST_COVERAGE_REPORT.md`
>
> **Modified:** `WitnessDesktop.sln`, `.env`

> **2026-03-02 Phase 05: Brain Infrastructure Complete.** Full brain pipeline implemented across 4 plans in 3 waves. IBrainService + OpenRouter REST client for vision + tool calling analysis. BrainResult types with Channel<T> pipeline. ToolExecutor handles capture_screen, get_game_state, get_best_move, web_search locally. BrainEventRouter upgraded with async Channel consumer (StartConsuming/StopConsuming/RouteBrainResult). OpenRouterBrainService with multi-turn tool calling (max 5 turns). DI wiring selects production vs mock based on OPENROUTER_APIKEY env var. MainViewModel forwards captures to brain in parallel with voice provider. Code review identified and fixed: API key config collision (direct env read), CancelAll race condition (Interlocked.Exchange), error message sanitization (generic UI text), HttpClient DNS staleness (PooledConnectionLifetime), duplicate image compression. Verification: 20/20 must-haves passed.
>
> **New Files:** `Models/BrainResult.cs`, `Models/OpenRouterModels.cs`, `Services/IBrainService.cs`, `Services/Brain/MockBrainService.cs`, `Services/Brain/OpenRouterClient.cs`, `Services/Brain/ToolExecutor.cs`, `Services/Brain/OpenRouterBrainService.cs`
>
> **Modified:** `Services/IBrainEventRouter.cs`, `Services/BrainEventRouter.cs`, `MauiProgram.cs`, `ViewModels/MainViewModel.cs`

> **2026-03-02 Ghost Mode Final Polish:** Card-to-FAB anchoring finalized — cards always display LEFT of FAB with 14pt clearance, right-aligned to absolute boundary (`fabOrigin.x - cardGap`). When FAB dragged left, cards shrink horizontally rather than crossing the boundary. `layoutAllCards()` is single source of truth; show methods animate alpha only. `removeAllAnimations()` strips in-flight animations before repositioning during drag. Added balanced bottom padding to message cards (text + text+image variants) with centered xmark SF Symbol (16pt, white @ 0.4 alpha) for immediate dismiss. Stack gap between audio and message cards increased to 12pt. Audio and message cards display/dismiss independently (no mutual exclusion). Ghost Mode overlay UI is now finalized.
>
> **Modified:** `EventCardView.swift` (bottom padding + X dismiss icon), `GhostPanelContentView.swift` (card anchoring fix, layout engine)

> **2026-03-02 MainView Production Polish Session:** Comprehensive UI polish bringing the MainView near production quality. Created industrial toggle switch controls (vertical + horizontal) with metallic gradients, LED bars, spring animations. Replaced Connect button with horizontal toggle, added 3-switch Audio Control Panel (MIC/AUTO/AI-MIC). MIC toggle syncs bidirectionally with ConnectionState. Created AudioLevelMeter (LED VU meter) with idle pulse animation for both agent and player profiles. Flipped timeline scroll (newest on top). Restyled Ghost FAB to metallic gray with green/yellow indicator ring. Changed preview box to black bg with red border when streaming. Made live chat window stroke-only (transparent bg). Applied 1.5x chat scaling. Matched send button to metallic gray design language. Cleaned up dividers. Refined profile layouts to clean 3-line stacks (Name/Subtitle/VAD) next to square images.
>
> **New Files:** `Controls/IndustrialToggleSwitch.xaml+.cs`, `Controls/HorizontalIndustrialToggle.xaml+.cs`, `Controls/AudioLevelMeter.cs`, `Views/AudioControlPanelView.xaml+.cs`, `Resources/Images/speaker_high.svg`, `Resources/Images/microphone.svg`
>
> **Modified:** `MainPage.xaml`, `MainPage.xaml.cs`, `ViewModels/MainViewModel.cs`, `Views/FabOverlayView.xaml`, `Views/TimelineView.xaml`, `Views/DirectMessageBubble.xaml`, `Views/ProactiveAlertView.xaml`, `Services/TimelineFeed.cs`, `Models/Timeline/TimelineCheckpoint.cs`

> **2026-02-27 Ghost Mode UI Polish Session:** Major visual refinements to the native ghost overlay after Phase 04 completion. All changes in pure AppKit (EventCardView.swift, FabView.swift, GhostPanelContentView.swift) + MAUI layout (MainPage.xaml, FabOverlayView.xaml, Agent.cs):
>
> **FAB Button States (3 visual modes):**
> - Off-Game: Yellow circle + "GHOST MODE" black text (CATextLayer, .heavy weight)
> - In-Game: Agent close-up portrait + yellow glow ring
> - Active: Same as in-game (removed X icon per design inspiration)
> - All states full opacity (removed 0.3 dim on off-game)
>
> **FAB Repositioned to Top-Right (HUD style):**
> - Default position: topmost rightmost corner with 16pt padding
> - Draggable: mouseDragged handler with 4pt threshold (tap vs drag). FAB clamps to screen bounds. Card follows FAB position.
>
> **Card Speech Bubble Layout:**
> - Card appears to the LEFT of FAB (speech bubble style), vertically centered on the button
> - 20pt gap prevents card from occluding FAB
> - Slide-in animation from right with 0.25s ease-in-out
>
> **Card Visual Enhancements:**
> - Translucent background: alpha 0.95 -> 0.55 (game visible through card)
> - SF Symbol message type icons: `text.bubble.fill` (text cards), `photo.on.rectangle` (image cards), 24pt cyan
> - Thin dividers (white 15% opacity) above and below message text
> - Bold text: 15pt .bold weight (up from 13pt .regular)
> - Centered text alignment
> - Removed "AI INSIGHT" title pill (pass null for title)
>
> **Bottom Bar Layout Restructure (MainPage.xaml):**
> - 3-column: [User Profile 340px] [Agent Panel *] [FAB Auto]
> - FAB in its own column on bare background (not sitting inside dark panel)
> - FAB scaled to 100/90pt to match user profile circle size
> - "GHOST MODE" text: OrbitronBold 15pt black
>
> **Agent Portrait Fixes:**
> - Derek: Swapped IconImage/PortraitImage (close-up was in wrong property)
> - Leroy: Added leroy_profile_pic.png (close-up portrait from assets/)
>
> **Framework Packaging Fix:**
> - xcframework regeneration lost .framework bundle structure (had bare dylib instead)
> - Rebuilt with proper Versions/A/ layout, symlinks, Info.plist to match csproj CopyGhostModeFramework target expectations

> **2026-02-27 Phase 04 Ghost Mode — COMPLETE:** All 3 GSD plans executed successfully. Native floating overlay renders over fullscreen games -- a competitive differentiator for the product. Architecture summary:
> - **GaimerGhostMode.xcframework:** Pure AppKit implementation (NSPanel + NSView + CALayer + Core Animation). 16 @_cdecl exports for C# interop. Universal binary (arm64 + x86_64). Built for macOS, then vtool binary-retagged to Mac Catalyst platform tag.
> - **C# Interop:** IGhostModeService abstraction with MacGhostModeService (production) and MockGhostModeService (testing). DllImport P/Invoke declarations. GCHandle callback pinning. Shared DllImportResolver in NativeMethods.cs handles both GaimerScreenCapture and GaimerGhostMode frameworks.
> - **Integration:** FAB overlay button on MainPage footer (agent portrait, yellow glow when connected, toggles ghost mode). MainViewModel orchestrates ghost mode toggle and event forwarding. csproj post-build target copies xcframework. DI registration in MauiProgram.cs.
> - **Ghost Mode Flow:** FAB tap -> MacGhostModeService.EnterGhostModeAsync -> hide MAUI window via NSWindow.orderOut -> ghost_panel_show (NSPanel appears with FAB + event cards). Exit: FAB tap on overlay -> ghost_panel_hide -> NSWindow.makeKeyAndOrderFront -> MAUI window restored.
> - **UI Features:** Click-through transparent areas (ClickThroughHostingView selective hit-test), auto-dismiss event cards, voice/text/image card variants, smooth fade animations.
>
> **Three Critical Bugs Solved:**
> 1. **SwiftUI NSHostingView symbol missing:** SwiftUI cannot be used in macOS-built frameworks loaded into Catalyst -- the iOS SwiftUI runtime lacks NSHostingView. Fix: rewrote all ghost mode UI in pure AppKit (NSView subclasses + CALayer + Core Animation). No SwiftUI dependency.
> 2. **DispatchQueue deadlock:** @MainActor annotation on Swift functions caused deadlock when called from C# Task.Wait() on the main thread. Fix: replaced @MainActor with explicit DispatchQueue.main.async dispatch, allowing the native call to return immediately while UI work queues asynchronously.
> 3. **UIWindow chrome visible during ghost mode:** Calling NSApplication.shared.hide() or window.miniaturize() left window chrome or dock artifacts. Fix: NSWindow.orderOut(nil) completely removes the MAUI window from the screen without side effects; NSWindow.makeKeyAndOrderFront(nil) restores it cleanly.
>
> **Key Architectural Decision:** vtool binary retagging. GaimerGhostMode must be built as a macOS framework (AppKit's NSPanel is unavailable on Mac Catalyst), but MAUI runs as a Catalyst app. vtool rewrites the Mach-O platform tag from macOS to Mac Catalyst, allowing the linker to load it. This is the same technique used by Apple's own bridging tools.

> **2026-02-24 Phase 02 Chat Brain Architecture — COMPLETE:** All 6 GSD plans executed successfully. Full integration wiring completed after initial assessment. Architecture includes:
> - **Core Models:** SessionContext, MessageRole/Intent, Timeline hierarchy (Checkpoint→EventLine→Event), BrainMetadata, ToolDefinition, BrainHint
> - **Session State Machine:** ISessionManager with OutGame/InGame states, tool availability gating by session state
> - **Brain Event Router:** IBrainEventRouter wired to MainViewModel - routes OnScreenCapture, OnDirectMessage, OnImageAnalysis to timeline and voice consumers
> - **Timeline Feed Manager:** ITimelineFeed with checkpoint creation, event stacking by output type
> - **Chat Prompt Builder:** IChatPromptBuilder with dynamic system prompt assembly, session context injection
> - **Timeline UI:** TimelineView.xaml with DataTemplateSelector, DirectMessageBubble, ProactiveAlertView with urgency DataTriggers
> - **Integration:** BrainEventRouter injected and called on frame captures + text exchanges, TimelineView replaces flat CollectionView in MainPage
> - **Converters:** TimelineEventTemplateSelector, RoleToUserVisibilityConverter, urgency styling (high=red, medium=yellow, low=green)
> - **Build verified:** C# compilation successful (DLL built)
>
> **Files Created (18):**
> - Models: SessionContext.cs, BrainMetadata.cs, ToolCallInfo.cs, BrainHint.cs, ToolDefinition.cs
> - Models/Timeline: EventOutputType.cs, ChessEventType.cs, RpgEventType.cs, FpsEventType.cs, TimelineCheckpoint.cs, EventLine.cs, TimelineEvent.cs, EventIconMap.cs
> - Services: ISessionManager.cs, SessionManager.cs, ITimelineFeed.cs, TimelineFeed.cs, IBrainEventRouter.cs, BrainEventRouter.cs, IChatPromptBuilder.cs, ChatPromptBuilder.cs
> - Views: TimelineView.xaml/.cs, DirectMessageBubble.xaml/.cs, ProactiveAlertView.xaml/.cs
> - Utilities: TimelineConverters.cs, TimelineEventTemplateSelector.cs
>
> **Files Modified:** MainViewModel.cs (9 changes), MauiProgram.cs (DI), MainPage.xaml, App.xaml (converters), ChatMessage.cs, Agent.cs, AgentSelectionViewModel.cs

> **2026-02-24 Message Routing Refinement:** Fixed TextReceived handler in MainViewModel to differentiate between direct chat replies and commentary/insights:
> - **Direct Chat Replies** (when `_pendingUserMessage != null`): Route to Timeline bubbles ONLY. Skip Live Chat Bar and Sliding Panel updates — keeps conversation private to timeline context.
> - **Commentary/Insights** (image analysis, proactive alerts): Update ALL surfaces — Live Chat Bar, Sliding Panel, and Timeline.
> - Added `MessageIntent` to ChatMessage: `GeneralChat` for direct chat, `ImageAnalysis` for commentary.
> - **Rationale:** Live Chat Bar should remain a "proactive insight ticker" for short alerts, not echo full conversation turns.

> **2026-02-24 Plan 01 Core Models Implemented:** Executed GSD Phase 02, Plan 01. Created all foundation data structures for Chat Brain Architecture:
> - **Agent model:** Added `AgentType` enum and `IsAvailable` flag for feature gating. Only Leroy (Chess) available initially.
> - **Session models:** `SessionContext.cs` with `SessionState` enum (OutGame/InGame)
> - **Message models:** Updated `ChatMessage.cs` with `MessageRole` (User/Assistant/System/Proactive), `MessageIntent`, `BrainMetadata`, `ToolCallInfo`. Old properties marked `[Obsolete]`.
> - **Timeline models:** Created `Timeline/` folder with 7 files: `EventOutputType.cs` (11 generic + AgentSpecific marker), `ChessEventType.cs` (2), `RpgEventType.cs` (3), `FpsEventType.cs` (placeholder), `TimelineEvent.cs`, `EventLine.cs`, `TimelineCheckpoint.cs`
> - **Icon mapping:** `EventIconMap.cs` resolves correct icons with no reuse policy (16 unique assets)
> - **Agent selection:** `AgentSelectionViewModel` now uses `Agents.Available` instead of `Agents.All`
> - **Build status:** Code compiles successfully (codesign issue is pre-existing tooling problem, not code error)
>
> **2026-02-24 Event Model Finalized:** Updated Plan 01 (Core Models) with simplified event architecture:
> - **Generic events (11):** Danger, Opportunity, SageAdvice, Assessment, GameStateChange, Detection, DirectMessage, ImageAnalysis, AnalyticsResult, HistoryRecall, GeneralChat
> - **Chess events (2):** PositionEval, Tactic
> - **RPG events (3):** QuestHint, Lore, ItemAlert
> - **No icon reuse policy:** Each event type has unique asset (16 total icons verified in `assets/`)
> - **Detection** added as generic user-primed vision capability
> - **SageAdvice** consolidates tactical guidance (BestMove merged in)
> - Agent feature gating: only Leroy (Chess) available initially
>
> **2026-02-24 Chat Brain Architecture (GSD Phase 02):** Created comprehensive GSD phase based on Chat Brain Design spec from `~/Desktop/agentic office/RemoteAgents/Gaimer-Desktop-Brain/chat-brain-design.md`. Phase includes 6 plans (25-37 hours estimated):
> - **01-core-models:** SessionContext, MessageRole/Intent, Timeline hierarchy (Checkpoint→EventLine→Event), BrainMetadata
> - **02-session-state-machine:** OutGame/InGame states with tool availability gating
> - **03-brain-event-router:** Central hub routing Brain output to Timeline, Voice, Top Strip
> - **04-timeline-feed-manager:** Checkpoint creation, event stacking by type
> - **05-chat-prompt-builder:** Dynamic system prompt with session context injection
> - **06-timeline-ui:** XAML timeline pattern with checkpoint headers, event icons, proactive alert styling
>
> **Key architectural changes from design spec:**
> - `MessageRole.Proactive` distinct from `Assistant` (renders with signal badge, urgency accent)
> - Checkpoint-based timeline replaces flat chat list (new capture = new section)
> - `BrainEventRouter` as single source of truth for all Brain output
> - Session state machine gates tool availability (InGame adds CaptureScreen, GetBestMove, etc.)
> - Chat system prompt adapts to session context dynamically
>
> **Source specs:** `chat-brain-design.md`, `brain-and-tools-reference.md`, `BRAIN_CONTEXT_PIPELINE_SPEC.md`
> **GSD files:** `.planning/phases/02-chat-brain/ROADMAP.md` + 6 plan files

> **2026-02-23 Asset Organization:** Created `assets/` folder as git-tracked source of truth for design assets. Moved all PNG icons from root. Renamed `default_agent_image.png` to `leroy-chessmaster-profile-icon.png`. Deleted SVG files (camera.svg, chess.svg). Merged `ref-images/` into assets. Strategy: assets/ = source designs, Resources/Images/ = MAUI-compatible copies with underscore naming.

> **2026-02-23 Agent Selection Card Redesign:** Complete redesign of AgentSelectionPage with bold, gamey agent cards. Cards now feature full portrait images on left (Derek, Leroy) with details on right, color-themed glows (green for Derek/Adventurer, amber for Leroy/Chess Master), chevron navigation with directional indicators (white=can navigate, gray=boundary), centered focal layout (logo → card → label), and responsive spacing. Agent model updated with `PortraitImage` property for selection cards and `IconImage` for profile displays. Added gaimer logo header. Removed index dots in favor of chevron-based navigation indicators.

> **2026-02-23 UI Polish Session 2:** Left panel redesign complete. Added thin horizontal divider lines between top/main/bottom sections. Redesigned agent profile to use actual image (default_agent.png) with audio output bars (10 squares, yellow/red gradient for voice power). Redesigned user profile with matching audio input bars and "Listening/Speaking" status. Restructured preview section: camera PNG header with streaming indicator (gray/green dot + label), empty preview box with focus.png, source label positioned outside box. Replaced "Active Application" card with clean "Connectors" section — header with connectors.png icon + white "Connectors" label, Chess.com connector pill with chess icon, status label, and Connect/Disconnect button. Connected state shows green accents (border, status text). All PNG assets added to Resources/Images.

> **2026-02-23 UI Design Session:** Major MainView layout restructuring. Replaced header with agent profile square + live message bar. Removed sidebar preview/app-list header clutter. Implemented 60/40 chat/observation split (reverted — observation panel deferred). Unified chat messages + input into single card container. Font sizes bumped ~2x across all elements for MacCatalyst readability. Chat input switched to slim `Entry` inside the card. **macOS run workaround documented:** app must be copied to `/tmp`, ad-hoc signed, then opened (File Provider xattr issue on Documents path).

> **2026-02-23 Tooling & Skills Session:** Installed **Superpowers MCP plugin** to global Cursor config (`~/.cursor/mcp.json`) — provides expert-crafted workflow skills (TDD, debugging, code review, brainstorming) via `find_skills`/`use_skill`/`get_skill_file` tools. Researched code reviewer MCP options (Anthropic plugin directory, community MCP servers). Created **`frontend-design-aesthetic` Cursor skill** (`~/.cursor/skills/frontend-design-aesthetic/SKILL.md`) — adapted from Anthropic's official `frontend-design` plugin to provide creative direction and visual design philosophy, complementing the existing `component-design-patterns` (technical how-to) and `implement-design` (Figma pixel-perfect) skills. Updated CURSOR_SUBAGENT_ROSTER with new skill stack.

> **2026-02-20 UI Session 2:** Fixed agent-selection navigation crash (invalid `KeyboardFlags` value `Suggestions,CapitalizeSentences` in MainPage.xaml crashed page creation). Overhauled palette to purple-main + cool-gray-accent + yellow-buttons hierarchy. Removed all decorative border strokes from MainPage. Removed text shadow blur effects. Brightened muted text for legibility. Cleaned debug instrumentation from AgentSelectionPage. **Chat feature wired but not yet tested end-to-end — UI polish first before proceeding with remaining GMR tickets.**

> **2026-02-20 Stabilization:** Fixed MainPage.xaml XAML compile errors (ItemsUpdatingScrollMode KeepLastItem→KeepLastItemInView, CollectionView Padding→Margin); registered ByteArrayToImageSourceConverter; implemented GMR-012 offline-aware chat (CanSendTextMessage, send button disabled when offline, "Connect to send messages" placeholder hint).

---

> ⚠️ **AGENT REMINDER:** When updating this log with implementation changes:
> 1. Check the **Spec Divergence Tracker** section below
> 2. Update `FEATURE_LIST.md` → "Spec Divergences (Tracked)" table
> 3. Mark resolved divergencies as ✅ in both documents
> 4. Add new divergencies if implementation differs from `gaimer_design_spec.md`

---

## Progress Overview

| Phase | Status | Start Date | End Date | Completion % |
|-------|--------|------------|----------|--------------|
| Phase 1: Foundation | ✅ UI Complete | Dec 10, 2024 | Dec 12, 2024 | 95% |
| Phase 2: Audio | ✅ Code Complete | Dec 13, 2025 | Dec 13, 2025 | 90% |
| Phase 3: Integration | ✅ Code Complete | Dec 13, 2025 | Dec 14, 2025 | 95% |
| Phase 02: Chat Brain | ✅ Complete | Feb 24, 2026 | Feb 24, 2026 | 100% |
| Phase 03: Screen Capture | ✅ Complete | Feb 25, 2026 | Feb 25, 2026 | 100% |
| Phase 04: Ghost Mode | ✅ Complete | Feb 26, 2026 | Feb 27, 2026 | 100% |
| Phase 05: Persistence | Not Started | TBD | TBD | 0% |
| Phase 06: Polish | Not Started | TBD | TBD | 0% |

**Overall Progress:** 75% Complete

> **Phase 2+3 Note:** Code complete; awaiting on-device testing (Windows + macOS). See `PHASE2_AUDIO_IMPLEMENTATION_PLAN.md` and `PHASE3_INTEGRATION_IMPLEMENTATION_PLAN.md` Testing Checklists.

> **Chat V2 MVP:** PM deliverable created 2026-02-20 — `WITNESS/gaimer_spec_docs/CHAT_V2_MVP_DELIVERABLE.md`. Defines MVP scope, P0/P1/P2 backlog, user stories, 2-phase execution plan (A: ~1 week, B: ~1–2 weeks).

### Phase 1 Blockers (Platform-Specific Implementation Required)
| Blocker | Description | Priority |
|---------|-------------|----------|
| Window Enumeration | Requires Windows EnumWindows / macOS CGWindowList | High |
| Window Capture | Requires Windows PrintWindow / macOS CGWindowListCreateImage | High |
| Audio Visualizer Animation | Requires SkiaSharp or platform audio APIs | Medium |

---

## Phase 1: Foundation (Week 1-2)

**Status:** In Progress  
**Target Completion:** Week 2

### Tasks
- [x] **Project Setup**
  - [x] Create .NET MAUI solution (WitnessDesktop.sln)
  - [x] Configure project structure (Views/, ViewModels/, Services/, Models/, etc.)
  - [x] Set up dependency injection (MauiProgram.cs)
  - [x] Configure NuGet packages (CommunityToolkit.Mvvm, SkiaSharp)
  - [x] Set up platform-specific projects

- [x] **Basic UI Layout**
  - [x] Create MainPage.xaml (Gaimer Dashboard)
  - [x] Implement color palette (Colors.xaml with Gaimer design tokens)
  - [x] Implement styles (Styles.xaml)
  - [x] Add custom fonts (Orbitron, Rajdhani)
  - [x] Create basic layout structure (AgentSelectionPage, MainPage, MinimalViewPage)

- [x] **Core Models & Services (Mock)**
  - [x] Create Agent model with General and Chess presets
  - [x] Create CaptureTarget, ConnectionState, SlidingPanelContent models
  - [x] Create IAudioService, IWindowCaptureService, IGeminiService interfaces
  - [x] Implement MockAudioService, MockWindowCaptureService, MockGeminiService
  - [x] Create AgentSelectionViewModel, MainViewModel, MinimalViewModel

- [x] **Agent Selection Screen**
  - [x] Logo and header with "Select Your Gaimer"
  - [x] Two agent cards: General Purpose and Chess
  - [x] Agent selection navigates to MainPage with agent context
  - [x] Footer with 5DOF AI Studio copyright

- [x] **Main Dashboard (MainView)**
  - [x] Header with logo, agent badge, connection status, settings button
  - [x] Three-column layout: Preview (left), AI Insights (center), Game Selector (right)
  - [x] Square preview box with LIVE indicator and game details
  - [x] AI Insights panel with scrollable text display
  - [x] Game selector with list items and selection highlighting (cyan border)
  - [x] Audio section with IN/OUT indicators (bars not animating - BUG-001)
  - [x] Footer with agent info, version, fixed-width Connect button with spinner
  - [x] Connect button disabled until game selected
  - [x] Auto-navigation to MinimalView on successful connection

- [x] **MinimalView (Complete - Redesigned Dec 12, 2024)**
  - [x] Wide layout (960×350) for better message display
  - [x] Agent profile with game info (header)
  - [x] Audio level indicators (IN/OUT percentages)
  - [x] Expand button (⤢) to return to MainView while connected
  - [x] Proper state sharing with MainView (Singleton MainViewModel)
  - [x] Window resizing: 960×350 compact, 1200×900 default expanded (resizable; min 900×720)
  - [x] **Inline message display** (centered, 20pt font, tap to dismiss)
  - [x] Auto-dismiss timer (5 seconds default)
  - [x] LIVE indicator (bottom left, no background)
  - [x] Audio visualizer bars placeholder (bottom center)
  - [x] Disconnect button (bottom right, smaller)
  - [x] BUG-002 FIXED: State persists across navigation

- [ ] **Window Enumeration**
  - [x] Create IWindowCaptureService interface
  - [ ] Implement Windows window enumeration (EnumWindows)
  - [ ] Implement macOS window enumeration (CGWindowList)
  - [x] Create CaptureTarget model
  - [ ] Filter system processes
  - [ ] Generate window thumbnails

- [ ] **Window Capture (Basic)**
  - [ ] Implement Windows capture (PrintWindow)
  - [ ] Implement macOS capture (CGWindowListCreateImage)
  - [ ] JPEG conversion and compression
  - [ ] Image scaling (50%)
  - [x] Capture timer (1 FPS) - mock implementation

### Progress Notes
- **Dec 10, 2024:** Stage 0 & 1 complete. Environment verified (.NET 8.0.412, MAUI workloads). UI mockup tested. Solution created with full project structure. All mock services implemented. Build succeeds on macOS (with `-p:EnableCodeSigning=false` for dev).
- **Dec 10, 2024:** Initial commit pushed to GitHub (https://github.com/IkeGister/Gaimer-app). Stage 2 (UI Build-Out) commenced.
- **Dec 10, 2024:** Stage 2 complete. Added Orbitron/Rajdhani fonts, Gaimer dark theme styles, polished all three views (AgentSelection, Dashboard, MinimalView) to match HTML mockup.
- **Dec 10, 2024:** Stage 3 complete. DI wiring configured, navigation flow working (AgentSelection → Dashboard → MinimalView), mock services registered as singletons. Ready for test build.
- **Dec 10, 2024 (Evening):** Major UI polish session:
  - MainView redesigned with three-column layout (Preview | AI Insights | Game Selector)
  - Square preview box with left-aligned game details
  - AI Insights panel with scrollable content, proper empty/content state switching
  - Game selection highlighting with cyan border
  - Fixed-width Connect button (140px) with spinner during connection
  - Connect requires both agent AND game selection
  - Auto-navigation to MinimalView on connect
  - Removed audio visualizer placeholder
  - Cleaned up AgentSelectionPage footer (removed version badge and API text)
  - Identified two bugs: audio bars not animating (BUG-001), disconnect on MinimalView return (BUG-002)

---

## Phase 2: Audio (Week 2-3)

**Status:** Not Started  
**Target Completion:** Week 3

### Tasks
- [ ] **Microphone Capture (Windows)**
  - [ ] Create IAudioService interface
  - [ ] Implement WASAPI capture via NAudio
  - [ ] Configure 16kHz, 16-bit, mono format
  - [ ] Implement resampling if needed
  - [ ] Add volume level calculation (RMS)
  - [ ] Handle device changes

- [ ] **Microphone Capture (macOS)**
  - [ ] Implement AVAudioEngine capture
  - [ ] Configure 16kHz, 16-bit, mono format
  - [ ] Implement PCM extraction
  - [ ] Add volume level calculation (RMS)
  - [ ] Handle permission requests

- [ ] **Audio Playback (Windows)**
  - [ ] Implement WASAPI playback via NAudio
  - [ ] Configure 24kHz, 16-bit, mono format
  - [ ] Implement buffered playback
  - [ ] Add interrupt capability
  - [ ] Handle device changes

- [ ] **Audio Playback (macOS)**
  - [ ] Implement AVAudioEngine playback
  - [ ] Configure 24kHz, 16-bit, mono format
  - [ ] Implement buffer scheduling
  - [ ] Add interrupt capability

### Progress Notes
_No progress yet_

---

## Phase 3: Integration (Week 3-4)

**Status:** ✅ Code Complete (Testing Pending)  
**Target Completion:** Week 4

### Tasks
- [x] **WebSocket Client**
  - [x] Update IGeminiService interface (IDisposable, ErrorOccurred, IsConnected)
  - [x] Implement ClientWebSocket wrapper (GeminiLiveService)
  - [x] Connection state management (Disconnected/Connecting/Connected/Reconnecting/Error)
  - [x] Message sending/receiving (audio/image + response processing)
  - [x] Error handling + auto-reconnect with backoff

- [x] **Gemini API Integration**
  - [x] Implement connection setup message
  - [x] Configure model (gemini-2.5-flash-preview-native-audio-dialog)
  - [x] Configure voice (Fenrir)
  - [x] Implement system prompt (Agent.SystemInstruction)
  - [x] Handle API responses (audio/text/interrupted)

- [x] **Audio/Image Sending**
  - [x] Implement SendAudioAsync (PCM to base64)
  - [x] Implement SendImageAsync (JPEG to base64)
  - [x] Proper MIME type specification (audio/pcm;rate=16000, image/jpeg)
  - [x] Message formatting (realtime_input.media_chunks)

- [x] **Response Handling**
  - [x] Parse JSON WebSocket messages (assembled by EndOfMessage)
  - [x] Extract audio data from responses (inlineData base64 decode)
  - [x] Handle interruption signals (serverContent.interrupted)
  - [x] Route audio to playback service (AudioReceived -> IAudioService.PlayAudioAsync)

- [x] **MainViewModel Integration**
  - [x] Connect all services (DI + IConfiguration + real/mock selection)
  - [x] Implement Connect/Disconnect logic
  - [x] Wire up events (AudioReceived, TextReceived, Interrupted, ErrorOccurred)
  - [x] Handle state changes (including Reconnecting in UI)

### Progress Notes
- **Dec 15, 2025:** OpenAI Realtime provider integration (commit `3e8c0df`):
  - Added WASP agent preset (FPS co-pilot for Fortnite/CoD with British accent personality)
  - Implemented `OpenAIRealtimeService` WebSocket client with session.update, input_audio_buffer.append, response.audio.delta
  - Created `OpenAIConversationProvider` adapter with 16kHz→24kHz audio resampling
  - Updated `ConversationProviderFactory` to create OpenAI provider when `OPENAI_APIKEY` is present
  - Added `OPENAI_` environment variable prefix in MauiProgram.cs
  - Provider selection: `VOICE_PROVIDER=openai` for explicit, or auto-detect based on available API keys (Gemini prioritized over OpenAI)
  
  **Audio Sampling Research Findings:**
  | Format | Input Rate | Output Rate | Notes |
  |--------|------------|-------------|-------|
  | `pcm16` | 24kHz | 24kHz | Default high-quality format |
  | `g711_ulaw` | 8kHz | 8kHz | Telephony, low bandwidth |
  | `g711_alaw` | 8kHz | 8kHz | Telephony, low bandwidth |
  
  **Current Implementation:** Resampling 16kHz→24kHz in `OpenAIConversationProvider.SendAudioAsync()`
  
  **Research Conflict:** Some sources indicate OpenAI Realtime accepts 16kHz input directly with `pcm16` format. If audio quality is poor or latency is high during testing, try removing the resampling step and sending 16kHz directly.
  
  **Options if issues arise:**
  1. Keep resampling (current) - safer, ensures 24kHz compliance
  2. Remove resampling - send 16kHz directly if OpenAI accepts it
  3. Use `g711_ulaw` format - lower quality but guaranteed 8kHz support

- **Dec 15, 2025 (Audio Playback Fix):** Fixed BUG-007, BUG-008, and BUG-009 (commit `1a075d8`):
  - **BUG-007 (Crash):** Pre-initialize player node before engine starts
  - **BUG-008 (Slow Playback):** Manual linear interpolation resampling 24kHz → 44.1kHz in `PlaybackService`
  - **BUG-009 (Echo):** Echo suppression (`!IsPlaying` check) + correct playback speed
  - **Key Fix:** Use mixer's native format (44.1kHz Float32 stereo) for connection, convert buffers before scheduling
  - **Files Modified:** `Platforms/MacCatalyst/PlaybackService.cs`
  - **Result:** ✅ Smooth, stable audio playback at correct speed; no echo

- **Dec 14, 2025:** Phase 3 code integrated:
  - Added configuration sources (user-secrets + GEMINI_ environment variables) and conditional DI for real vs mock Gemini service.
  - Implemented `GeminiLiveService` WebSocket client: setup, audio/image send, receive loop with message assembly, interruption handling.
  - Added auto-reconnect with backoff; improved teardown/dispose safety.
  - Wired MainViewModel to send mic PCM to Gemini and play AI PCM responses; forwards interruption to audio playback interrupt.
  - Updated `config.example.txt` with API key setup instructions.
  - Added `USE_MOCK_SERVICES` environment variable override for forcing mock mode.

---

## Phase 4: Polish (Week 4-5)

**Status:** Not Started  
**Target Completion:** Week 5

### Tasks
- [ ] **UI Styling (Gamer Theme)**
  - [ ] Refine color scheme
  - [ ] Add gradients and effects
  - [ ] Improve typography
  - [ ] Add animations/transitions
  - [ ] Polish button styles
  - [ ] Enhance visual feedback

- [ ] **Audio Visualizer**
  - [ ] Create AudioVisualizer control (SkiaSharp)
  - [ ] Implement waveform rendering
  - [ ] Bind to volume levels
  - [ ] Add visual effects

- [ ] **Error Handling**
  - [ ] Implement reconnection strategy
  - [ ] Add user-friendly error messages
  - [ ] Handle audio device errors
  - [ ] Handle capture errors
  - [ ] Implement resource cleanup

- [ ] **Testing**
  - [ ] Unit tests for services
  - [ ] Integration tests
  - [ ] Platform-specific testing (Windows)
  - [ ] Platform-specific testing (macOS)
  - [ ] End-to-end testing
  - [ ] Performance testing

- [ ] **Documentation**
  - [ ] Code comments
  - [ ] API documentation
  - [ ] User guide
  - [ ] Developer guide
  - [ ] Windows click-through overlay (PC gamers) — see `WITNESS/gaimer_spec_docs/WINDOWS_OVERLAY_IMPLEMENTATION_PLAN.md`

### Progress Notes
_No progress yet_

---

## Phase 5: Distribution (Week 5-6)

**Status:** Not Started  
**Target Completion:** Week 6

### Tasks
- [ ] **Windows Packaging**
  - [ ] Configure Package.appxmanifest
  - [ ] Set capabilities (microphone, graphicsCapture)
  - [ ] Create MSIX package
  - [ ] Test installation
  - [ ] Verify permissions

- [ ] **macOS Packaging**
  - [ ] Configure Entitlements.plist
  - [ ] Configure Info.plist
  - [ ] Set permissions (microphone, screen capture)
  - [ ] Create DMG package
  - [ ] Create PKG installer (optional)
  - [ ] Test installation

- [ ] **Code Signing**
  - [ ] Obtain Windows certificate (Authenticode)
  - [ ] Obtain Apple Developer ID
  - [ ] Sign Windows MSIX
  - [ ] Sign macOS DMG/PKG
  - [ ] Verify signatures

- [ ] **Distribution**
  - [ ] Create release notes
  - [ ] Prepare distribution channels
  - [ ] Version tagging
  - [ ] Release documentation

### Progress Notes
_No progress yet_

---

## Milestones

### Milestone 1: Foundation Complete
**Target Date:** End of Week 2  
**Status:** In Progress (90%)

**Deliverables:**
- ✅ Working project structure
- ✅ Basic UI layout for all three views
- ✅ Agent selection and navigation flow
- ✅ MainView with mock data and connection simulation
- ✅ MinimalView fully working with shared state
- ✅ Window resize on view transitions
- ✅ Sliding panel with animations
- ❌ Real window enumeration (mock only)
- ❌ Real window capture (mock only)

---

### Milestone 2: Audio Complete
**Target Date:** End of Week 3  
**Status:** ✅ Code Complete (Testing Pending)

**Deliverables:**
- ✅ Microphone capture implemented on both platforms (Windows NAudio, macOS AVAudioEngine)
- ✅ Audio playback implemented on both platforms
- ✅ Volume monitoring functional (RMS-based, VolumeChanged event)
- ⏳ On-device validation pending

---

### Milestone 3: Integration Complete
**Target Date:** End of Week 4  
**Status:** ✅ Code Complete (Testing Pending)

**Deliverables:**
- ✅ WebSocket connection to Gemini API (GeminiLiveService)
- ✅ Audio/image transmission working (base64 encoding, proper MIME types)
- ✅ AI responses playing back (AudioReceived → PlayAudioAsync)
- ✅ Interruption handling (serverContent.interrupted → InterruptPlaybackAsync)
- ✅ Auto-reconnect with backoff
- ⏳ End-to-end validation with real API pending

---

### Milestone 4: Polish Complete
**Target Date:** End of Week 5  
**Status:** Not Started

**Deliverables:**
- Polished UI with gamer theme
- Audio visualizer working
- Comprehensive error handling
- Testing complete

---

### Milestone 5: Release Ready
**Target Date:** End of Week 6  
**Status:** Not Started

**Deliverables:**
- Signed packages for both platforms
- Installation tested
- Documentation complete
- Ready for distribution

---

## Weekly Progress Updates

### Week 1 (Dec 10, 2024 - Dec 16, 2024)
**Status:** In Progress

**Completed:**
- Environment verification (.NET 8.0.412, MAUI workloads installed)
- UI mockup validation (HTML/CSS/JS prototype working)
- Solution and project structure created
- Core models: Agent, CaptureTarget, ConnectionState, SlidingPanelContent, AiDisplayContent
- Service interfaces: IAudioService, IWindowCaptureService, IGeminiService
- Mock service implementations for development
- ViewModels: AgentSelectionViewModel, MainViewModel, MinimalViewModel
- Views: AgentSelectionPage, MainPage (Dashboard), MinimalViewPage
- Gaimer design tokens in Colors.xaml
- Successful build on macOS
- **MainView UI polish complete:**
  - Three-column layout (Preview | AI Insights | Game Selector)
  - Game selection highlighting
  - Connect button with spinner and disabled state
  - Auto-navigation to MinimalView
  - AI message display in scrollable panel

**In Progress:**
- MinimalView navigation and state management fixes

**Known Bugs:**
- ~~BUG-001: Audio bars not animating (Medium severity)~~ **FIXED Dec 13, 2025**
- ~~BUG-002: Auto-disconnect on MinimalView return (High severity)~~ **FIXED Dec 12, 2024**

**Blockers:**
- macOS code signing requires `-p:EnableCodeSigning=false` for development builds

**Notes:**
- Stage 0, 1, 2, and 3 of implementation plan complete
- MainView matches design spec; MinimalView needs refinement
- Ready for bug fixes and MinimalView improvements

---

## Risk Register

| Risk | Impact | Probability | Mitigation | Status |
|------|--------|-------------|------------|--------|
| Platform API changes | High | Low | Monitor platform updates, use stable APIs | Open |
| Audio latency issues | Medium | Medium | Optimize buffer sizes, use native APIs | Open |
| Window capture permissions | High | Medium | Clear permission requests, documentation | Open |
| WebSocket stability | Medium | Low | Implement robust reconnection logic | Open |
| Build complexity | Low | Medium | Follow MAUI best practices, test early | Open |
| State management between views | Medium | High | Use shared services or state containers | **Resolved** |

---

## Spec Divergence Tracker

> 📋 **Maintenance:** When resolving a divergence, mark it ✅ here AND in `FEATURE_LIST.md`.
> When adding implementation that differs from spec, add a new row here.

This section tracks intentional or temporary differences between `gaimer_design_spec.md` and the actual implementation.

| Status | Component | Spec (gaimer_design_spec.md) | Actual Implementation | Reason | Resolution |
|--------|-----------|------------------------------|----------------------|--------|------------|
| ✅ | Main Dashboard Size | 820px × 720px | 1200px × 900px default (resizable; min 900×720) | macOS Catalyst sizing/restoration + better UX | Spec updated Dec 13, 2025 |
| ✅ | MinimalView Size | 480px × Auto | 960px × 350px | Wide format for inline messages | Spec updated Dec 12, 2024 |
| ✅ | MinimalView Message Display | Sliding panel (dropdown) | Inline centered display | Better UX, no jarring animations | Spec updated Dec 12, 2024 |
| ✅ | MinimalView Visualizer | Animated audio bars | Animated bars bound to live volume (`ActivityVolume`) | Implemented via XAML bindings (no Skia) | Optional Phase 4 SkiaSharp polish |
| ⏳ | Game Icon in MinimalView | Chess emoji (♟️) for chess games | Generic 🖥️ for all games | Chess detection not implemented | Phase 4: Game detection |

**Status Key:** ⏳ Pending | ✅ Resolved | ❌ Won't Fix

**Last Reviewed:** December 14, 2025

---

## Change Log

> 📝 **When adding entries:** If your changes implement features that differ from `gaimer_design_spec.md`,
> update the **Spec Divergence Tracker** above and `FEATURE_LIST.md` accordingly.

### December 9, 2024
- Created progress log
- Initialized all phases as "Not Started"
- Set up milestone tracking
- Created risk register

### December 9, 2024 (Gaimer)
- Added Gaimer implementation plan for Stages 1–3 under `WITNESS/gaimer_spec_docs/GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md`
- Defined early-phase workflow for environment setup, initial MAUI baseline, UI build-out with mock data, and first test build

### December 10, 2024 (Gaimer - Stage 0 & 1 Complete)
- Verified development environment: .NET 8.0.412, MAUI workloads, Git configured
- Validated UI mockup at localhost:8080
- Created `WitnessDesktop.sln` with full project structure
- Implemented all core models and mock services
- Created three main Views: AgentSelectionPage, MainPage, MinimalViewPage
- Added Gaimer design tokens to Colors.xaml
- Successful build on macOS with mock services
- Updated .gitignore for MAUI project

### December 10, 2024 (Gaimer - MainView UI Polish)
- Redesigned MainView layout: Preview (left) | AI Insights (center) | Game Selector (right)
- Implemented square preview box with left-aligned game details
- Added AI Insights panel with proper empty/content state management
- Game selection highlighting with cyan border
- Fixed-width Connect button (140px) with ActivityIndicator spinner
- Connect button disabled until game is selected
- Auto-navigation to MinimalView on successful connection
- Removed audio visualizer placeholder from both views
- Cleaned up AgentSelectionPage footer
- Identified and logged BUG-001 (audio bars) and BUG-002 (MinimalView disconnect)
- Updated Phase 1 completion to 75%

### December 12, 2024 (Gaimer - MinimalView Complete Implementation)
- **BUG-002 FIXED:** Auto-disconnect on MinimalView return resolved
- **State Sharing:** Made MainViewModel a Singleton for shared state between views
- **Window Resizing:** 
  - MinimalView: 480×250 compact size
  - MainView: 820×720 full size
  - Automatic resize on navigation
- **Sliding Panel Implementation:**
  - Added sliding panel UI to MinimalViewPage
  - Panel header with title and dismiss button
  - Scrollable text content area
  - Optional image display (scales to fit)
  - Progress bar indicator for auto-dismiss
- **Panel Animation:**
  - Slide-up animation (300ms, CubicOut easing)
  - Slide-down animation (250ms, CubicIn easing)
- **Auto-Dismiss Timer:**
  - 5-second default (configurable via AutoDismissMs)
  - Timer starts when panel appears
  - Uses MainThread.InvokeOnMainThreadAsync for thread safety
- **Navigation Flow:**
  - Expand button (⤢) returns to MainView while staying connected
  - Disconnect button disconnects and returns to MainView
- **Model Updates:**
  - Added ImageSource property to SlidingPanelContent
- **Architecture Decision:**
  - MinimalViewPage binds directly to MainViewModel (not MinimalViewModel)
  - MinimalViewModel refactored but unused - kept for potential future use
  - Removed MinimalViewModel DI registration from MauiProgram.cs
- **Files Modified:**
  - Models/SlidingPanelContent.cs
  - MauiProgram.cs (MainViewModel as Singleton, removed MinimalViewModel registration)
  - ViewModels/MainViewModel.cs (added ExpandToMainViewCommand, CurrentTarget, panel commands)
  - ViewModels/MinimalViewModel.cs (refactored to delegate - unused)
  - Views/MinimalViewPage.xaml (binds to MainViewModel)
  - Views/MinimalViewPage.xaml.cs (uses MainViewModel from DI, handles animations)
- Updated Phase 1 completion to 90%

### December 12, 2024 (MinimalView Redesign - Final)
- **Major UI Redesign:** MinimalView completely redesigned for better UX
- **Window Size:** 480×350 → 960×350 (doubled width for message display)
- **Layout Changes:**
  - Removed sliding panel dropdown
  - Messages now display inline in center content area (20pt font)
  - LIVE indicator moved to bottom left (no background)
  - Audio visualizer bars in bottom center
  - Disconnect button (smaller) at bottom right
  - Tap-to-dismiss for messages
- **Files Modified:**
  - `Views/MinimalViewPage.xaml` - Complete layout redesign
  - `Views/MinimalViewPage.xaml.cs` - Simplified (removed animation code)
  - `ViewModels/MainViewModel.cs` - Updated window size to 960×350
  - `gaimer_design_spec.md` - Updated Section 3.2 and 6.7 with new design
- **Spec Updates:** Documented new MinimalView layout as official spec
- **Phase 1 Status:** UI Complete (95%) - remaining blockers are platform-specific

### December 13, 2025 (Implementation Plans Created)
- **Created Phase 2 Implementation Plan:** `WITNESS/gaimer_spec_docs/PHASE2_AUDIO_IMPLEMENTATION_PLAN.md`
  - Comprehensive plan for audio system implementation (Windows WASAPI, macOS AVAudioEngine)
  - Includes microphone capture, audio playback, volume monitoring
  - Target completion: Week 2-3
- **Created Phase 3 Implementation Plan:** `WITNESS/gaimer_spec_docs/PHASE3_INTEGRATION_IMPLEMENTATION_PLAN.md`
  - Comprehensive plan for Gemini API integration
  - Includes WebSocket client, audio/image transmission, response handling
  - Target completion: Week 3-4
- **Removed macOS Catalyst window sizing entry:** Window size specification not honored; documented as known limitation
- **Next Priority:** Phase 2 - Audio hookup (microphone capture and playback)

### December 13, 2025 (Phase 2 Code Complete - Audio System)

> 🔖 **REVERT MARKER:** Commit `6d40496` — `feat(audio): Phase 2 code complete - platform audio services + spec contract`  
> If on-device testing reveals critical issues, run: `git revert 6d40496` or `git reset --hard 6a75f11`

- **Phase 2 Status:** ✅ **Code Complete** — Awaiting device validation
- **Aligned audio contract to spec:**
  - `IAudioService` updated to callback-based recording, combined `VolumeChanged`, `ErrorOccurred`, sample rate properties, `InterruptPlaybackAsync`, and `IDisposable`.
  - Added `VolumeChangedEventArgs` + `AudioErrorEventArgs` with full XML doc comments.
- **Updated consumers:**
  - `MainViewModel` now subscribes to `VolumeChanged` and uses callback-based `StartRecordingAsync(...)`.
- **Implemented platform audio services:**
  - Windows: `Platforms/Windows/AudioService.cs` (NAudio-based mic capture + playback, RMS, bounded buffer with overflow logging, interruption).
  - MacCatalyst: `Platforms/MacCatalyst/AudioService.cs` (AVAudioEngine-based mic capture + playback, RMS, interruption, permission request).
- **Updated platform config + DI:**
  - `Platforms/MacCatalyst/Info.plist`: `NSMicrophoneUsageDescription`
  - `Platforms/MacCatalyst/Entitlements.plist`: `com.apple.security.device.audio-input`
  - `Platforms/Windows/Package.appxmanifest`: `<DeviceCapability Name="microphone" />`
  - `MauiProgram.cs`: platform-conditional audio service registration (`#if WINDOWS / #elif MACCATALYST / #else Mock`)
  - `WitnessDesktop.csproj`: conditional `NAudio` reference for Windows target
- **Review Improvements Applied:**
  - Added XML doc comments to `IAudioService`, event args, and all `AudioService` implementations.
  - Windows playback buffer bounded to ~500ms with overflow detection/logging.
  - Windows microphone capability declared in manifest.
- **Deferred (Post-Testing / Phase 4):**
  - Resampling fallback (Windows) if device doesn't support 16kHz.
  - AVAudioEngine grace period (Mac) to avoid rapid start/stop overhead.
  - Output volume decay/smoothing (UI-layer concern).
- **Files Changed:**
  - `src/WitnessDesktop/WitnessDesktop/Services/IAudioService.cs`
  - `src/WitnessDesktop/WitnessDesktop/Services/VolumeChangedEventArgs.cs` (new)
  - `src/WitnessDesktop/WitnessDesktop/Services/AudioErrorEventArgs.cs` (new)
  - `src/WitnessDesktop/WitnessDesktop/Services/MockAudioService.cs`
  - `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`
  - `src/WitnessDesktop/WitnessDesktop/Platforms/Windows/AudioService.cs` (new)
  - `src/WitnessDesktop/WitnessDesktop/Platforms/Windows/Package.appxmanifest`
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/AudioService.cs` (new)
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/Info.plist`
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/Entitlements.plist`
  - `src/WitnessDesktop/WitnessDesktop/MauiProgram.cs`
  - `src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj`
  - `WITNESS/gaimer_spec_docs/PHASE2_AUDIO_IMPLEMENTATION_PLAN.md`
  - `WITNESS/FEATURE_LIST.md`
  - `WITNESS/PROGRESS_LOG.md`

### December 13, 2025 (Bug Fix - Audio Visualizer)
- **BUG-001 FIXED:** Audio bars now animate and IN/OUT values update reliably
- **Implementation:**
  - Marshal audio volume events onto UI thread in `MainViewModel`
  - Bind MinimalView visualizer bar heights to `ActivityVolume` via converter
  - Mock audio now emits non-silent 20ms PCM frames to avoid RMS flatline
- **Files Modified:**
  - `src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs`
  - `src/WitnessDesktop/WitnessDesktop/MainPage.xaml`
  - `src/WitnessDesktop/WitnessDesktop/Views/MinimalViewPage.xaml`
  - `src/WitnessDesktop/WitnessDesktop/Utilities/ValueConverters.cs`
  - `src/WitnessDesktop/WitnessDesktop/App.xaml`
  - `src/WitnessDesktop/WitnessDesktop/Services/MockAudioService.cs`

### December 13, 2025 (Navigation & Session Teardown Hardening)
- **MinimalView Expand:** Switched from tap-gesture on `Border` to a real `Button` for reliability on MacCatalyst.
- **Disconnect Teardown:** Disconnect now fully stops capture/audio and clears transient UI state (messages, preview, meters), so MainView returns to a true offline/idle presentation.
- **Navigation Stability:** Added a navigation lock + UI-thread window resize to avoid overlapping transitions and odd window sizing glitches.

### December 14, 2025 (Phase 3 Implementation Review & Documentation)

> 🔖 **REVERT MARKER:** Commit `bdf096a` — `feat(integration): Phase 3 code complete - Gemini Live API WebSocket client`  
> If on-device testing reveals critical issues, run: `git revert bdf096a` or `git reset --hard ea1f1d0`

- **Phase 3 Status:** ✅ **Code Complete** — Ready for on-device testing
- **Implementation Appraisal:** Excellent execution with quality improvements over plan
- **Quality Improvements Implemented:**
  - Thread safety: `SemaphoreSlim _connectLock` prevents race conditions
  - Cleanup isolation: `CleanupConnection_NoLock()` helper for safer teardown
  - Receive loop stability: Passes stable references to avoid closure issues
  - API key flexibility: Supports 5 different key configurations
  - Mock override: `USE_MOCK_SERVICES=true` environment variable
  - Error resilience: Try-catch around all WebSocket sends
  - Reconnect improvements: Proper cleanup before retry, disposed checks throughout
- **Files Implemented:**
  - `Services/GeminiLiveService.cs` (560 lines, new)
  - `Services/IGeminiService.cs` (updated with IDisposable, ErrorOccurred, IsConnected)
  - `Services/MockGeminiService.cs` (updated for new interface)
  - `Models/ConnectionState.cs` (added Reconnecting state)
  - `ViewModels/MainViewModel.cs` (wired all Gemini events)
  - `MauiProgram.cs` (configuration + conditional DI)
  - `WitnessDesktop.csproj` (added configuration packages)
  - `config.example.txt` (updated with API key instructions)
- **Testing Readiness:** Ready for on-device testing with API key
- **Documentation Updated:**
  - `PHASE3_INTEGRATION_IMPLEMENTATION_PLAN.md` - All tasks marked complete
  - `PROGRESS_LOG.md` - Updated phase status, milestones
  - `FEATURE_LIST.md` - Updated network layer features

### December 14, 2025 (On-Device Testing Aids)
- Added explicit runtime indicators/logging to confirm whether the app is using `GeminiLiveService` vs `MockGeminiService` (helpful when `.env` is present but not exported into the process environment).
- Fixed a race in auto-navigation so switching to MinimalView happens reliably when connection state reaches **Connected**.

#### December 14, 2025 (Test Session Commits)
- **Non-audio (UI/nav/DI indicators):** `db67387`
- **Audio-only (MacCatalyst AudioService):** `6fee3e7` (fix) / `8cb95be` (wip)
- **Test report:** `WITNESS/PHASE2_PHASE3_TEST_REPORT.md`

### December 14, 2025 (BUG-005 Fix - MacCatalyst Audio Session)

- **BUG-005 FIXED:** MacCatalyst microphone capture now works correctly
- **Root Cause:** AVAudioSession was not configured/activated before accessing AVAudioEngine's input node. MacCatalyst uses the iOS audio stack which requires explicit session management.
- **Symptoms:**
  - `inputNode.GetBusOutputFormat(0)` returned invalid format (`sampleRate=0`, `channelCount=0`)
  - `InstallTapOnBus` failed with: `required condition is false: IsFormatSampleRateAndChannelCountValid(format)`
  - No "hot mic" indicator, no audio frames captured
- **Fix Implementation:**
  1. Added `EnsureAudioSessionConfigured_NoLock()` method that:
     - Configures AVAudioSession with `PlayAndRecord` category
     - Sets preferred sample rate to 16kHz for Gemini compatibility
     - Sets preferred IO buffer duration for low latency (20ms)
     - **Activates** the session with `SetActive(true)` — this is critical
  2. Call session configuration **before** creating AVAudioEngine
  3. Call `_engine.Prepare()` after creation to allocate hardware resources
  4. Added format validation before installing tap with clear error message
  5. Added audio interruption observer for handling system interruptions (phone calls, Siri, etc.)
  6. Updated `Dispose()` to clean up observer and deactivate session
- **Technical Details:**
  - MacCatalyst inherits iOS audio behavior requiring explicit AVAudioSession management
  - Without `SetActive(true)`, the audio hardware isn't initialized and format queries return zeros
  - The session must be configured with proper category before activation
- **Files Modified:**
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/AudioService.cs`
  - `WITNESS/BUG_FIX_LOG.md`
  - `WITNESS/PROGRESS_LOG.md`
- **Testing:** ✅ VERIFIED WORKING (December 14, 2025)
  - `AVAudioSession category set to PlayAndRecord` ✅
  - `AVAudioSession activated. HW sample rate: 48000Hz` ✅
  - `AVAudioEngine created` ✅
  - `InputNode accessed. Format: 48000Hz` ✅
  - `Input tap installed (PCMFloat32/48000Hz/ch=1)` ✅
  - `Captured frame #1: 3200 bytes, rms=0.001` through #200+ ✅
- **Additional fixes during testing:**
  - Removed `Prepare()` call (throws if nodes not accessed first)
  - Replaced AVAudioConverter with manual float32→int16 conversion (AVAudioConverter had parameter errors with this format combo)

### December 14, 2025 (BUG-006 Fix - Gemini API Model Name)

- **BUG-006 FIXED:** Gemini Live API connection rejection
- **Issue:** Model name `gemini-2.5-flash-preview-native-audio-dialog` not found in v1beta API
- **Fix:** Changed to `gemini-2.0-flash-exp` which supports Multimodal Live API
- **Diagnostic logging added:**
  - Connection state changes
  - Setup message JSON content
  - WebSocket close status and description
  - Audio send/receive counters
- **External blocker:** API quota exceeded (account/billing issue, not code bug)
- **Status:** Phase 2 + 3 verified on macOS; blocked on quota reset for full end-to-end test
- **Deferred to Phase 4:** User-facing error messages for provider/account issues
- **Commit:** `2b9cfae`

### December 15, 2025 (OpenAI Realtime Provider - Testing Complete)

- **Commit:** `6789281` — Easy revert point (git revert 6789281)
- **OpenAI Realtime API Integration:** Successfully connected and tested end-to-end
- **Test Results:**
  - ✅ WebSocket connection to `wss://api.openai.com/v1/realtime` works
  - ✅ Session creation and update with agent instructions works
  - ✅ Audio capture (16kHz) → resampling (24kHz) → sending works
  - ✅ Server VAD speech detection works (OpenAI-side)
  - ✅ Audio response received and decoded
  - ✅ Transcription works (user speech → text, AI speech → text)
  - ✅ Clean disconnect (no crashes)
  - ⚠️ Audio playback works but ~1.8x slower than expected (sample rate conversion issue)
  - ⚠️ Echo suppression partial (AI hears itself due to slow playback)
- **Echo Suppression (Quick Fix):**
  - Added `!_audioService.IsPlaying` check in MainViewModel audio callback
  - Reduces echo but doesn't eliminate it (playback too slow = mic catches tail end)
- **BUG-007 Status:** Fixed (player node pre-initialized, format converter added)
- **Known Issues:**
  - Slow playback due to 24kHz → 44.1kHz conversion (1.8375x fractional ratio)
  - Echo leakage when AI speaks long responses
- **Next Step:** Implement separate recording/playback audio services for cleaner architecture
- **Files Modified:**
  - `ViewModels/MainViewModel.cs` — Added echo suppression check
  - `Platforms/MacCatalyst/AudioService.cs` — Fixed BUG-007 (player node init + converter)
- **Implementation Plan:** See `AUDIO_SEPARATION_IMPLEMENTATION_PLAN.md`

### December 16, 2025 (MacCatalyst Audio Separation - Implemented)

- **Goal:** Fix MacCatalyst slow OpenAI playback and reduce echo leakage by separating recording and playback into independent AVAudioEngines.
- **Implementation:** Added split audio services + composite wrapper so the rest of the app still consumes `IAudioService` unchanged.
  - Recording: dedicated AVAudioEngine input tap (native format) → normalize to 16kHz PCM16 mono
  - Playback: dedicated AVAudioEngine + AVAudioPlayerNode connected with explicit 24kHz PCM16 mono format; schedule buffers directly (no manual 24k→mixer conversion)
  - Session: ref-counted AVAudioSession lease manager so recording+playback can overlap safely
- **Expected Outcome:** Correct playback speed (BUG-008) and reduced mic capture window (helps BUG-009) — **pending on-device validation**
- **Files Added:**
  - `src/WitnessDesktop/WitnessDesktop/Services/Audio/IAudioRecordingService.cs`
  - `src/WitnessDesktop/WitnessDesktop/Services/Audio/IAudioPlaybackService.cs`
  - `src/WitnessDesktop/WitnessDesktop/Services/Audio/CompositeAudioService.cs`
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/MacAudioSessionManager.cs`
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/RecordingService.cs`
  - `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/PlaybackService.cs`
- **Files Modified:**
  - `src/WitnessDesktop/WitnessDesktop/MauiProgram.cs` — MacCatalyst DI now registers split services + composite `IAudioService`
- **Note:** Legacy `Platforms/MacCatalyst/AudioService.cs` remains in repo but is no longer used by DI.

### December 15, 2025 (Voice Provider Abstraction Layer)

- **Architecture Hardening:** Created provider-agnostic abstraction for voice AI services
- **Purpose:** Protect working Gemini implementation; enable OpenAI provider (and future multimodal providers)
- **New Files Created:**
  - `Services/Conversation/IConversationProvider.cs` — Provider-agnostic interface
  - `Services/Conversation/ConversationProviderFactory.cs` — Factory with env var selection logic
  - `Services/Conversation/Providers/GeminiConversationProvider.cs` — Adapter wrapping GeminiLiveService
  - `Services/Conversation/Providers/MockConversationProvider.cs` — Mock for testing without API keys
- **Modified Files:**
  - `MauiProgram.cs` — Registers `IConversationProvider` via factory; legacy `IGeminiService` retained for compatibility
  - `ViewModels/MainViewModel.cs` — Uses `IConversationProvider` instead of `IGeminiService`
- **Untouched Files:**
  - `GeminiLiveService.cs` — Working implementation protected via adapter pattern
  - `IGeminiService.cs` — Kept for backwards compatibility
- **Environment Variable Strategy:**
  - `VOICE_PROVIDER=gemini|openai|mock` (explicit selection)
  - Auto-detect based on API keys if not set
  - `USE_MOCK_SERVICES=true` forces mock mode
- **Future Provider Addition:** Create `XxxConversationProvider.cs`, add case to factory — no ViewModel changes needed
- **Design Decision:** Only multimodal providers (supporting text + audio + images/video) are supported to enable full visual coaching capabilities
- **ElevenLabs Removed (commit `9869f2f`):** Audio-only providers excluded as they cannot fulfill visual coaching requirements

### December 15, 2025 (Audio Format Normalization)

- **Audio Pipeline Decoupling:** Established standard audio contract independent of provider
- **Purpose:** Ensure audio service works with any provider regardless of native format
- **New Files Created:**
  - `Services/Audio/AudioFormat.cs` — Central constants: 16kHz input, 24kHz output, 16-bit, mono
  - `Services/Audio/AudioResampler.cs` — Utility for sample rate conversion, stereo→mono, float32→int16
- **Modified Files:**
  - `Services/Conversation/IConversationProvider.cs` — Added format contract documentation referencing `AudioFormat`
  - `Services/IAudioService.cs` — Linked to `AudioFormat` constants
- **Contract:**
  - Input (mic → provider): `AudioFormat.StandardInputSampleRate` (16kHz)
  - Output (provider → speaker): `AudioFormat.StandardOutputSampleRate` (24kHz)
- **Provider Responsibility:** Non-compliant providers must use `AudioResampler.ResampleToStandardOutput()` before emitting audio
- **Existing Providers:** Gemini (24kHz) and OpenAI (24kHz) already compliant
- **Commit:** Pending testing

---

### December 12, 2024 (Documentation Review & Cleanup)
- **Commit:** `5dd02df` - "refactor: cleanup MinimalViewModel, fix documentation inconsistencies"
- **Documentation Review:** Identified and fixed inconsistencies between implementation and docs
- **Code Cleanup:**
  - Removed unused MinimalViewModel DI registration from MauiProgram.cs
  - Added explanatory comments for architecture decision
- **Documentation Updates:**
  - MINIMALVIEW_IMPLEMENTATION_TASK.md: Updated "Files to Modify" → "Files Modified (Actual Implementation)"
  - BUG_FIX_LOG.md: Added details about MinimalViewModel being unused
  - PROGRESS_LOG.md: Added architecture decision notes
  - FEATURE_LIST.md: Updated timestamp
- **Status:** Pending testing on macOS to verify MinimalView navigation still works
- **Files Changed:** 10 files (483 insertions, 144 deletions)

---

### February 20, 2026 (Chat V2 P0 Slice: GMR-003, GMR-004, GMR-005)
- **Scope:** First vertical slice per `CHAT_V2_EXECUTION_TICKET_BOARD.md`.
- **GMR-003 SendTextAsync:** Added to `IConversationProvider`; implemented in `OpenAIConversationProvider` (conversation.item.create), `GeminiConversationProvider` (realtime_input + text/plain), `MockConversationProvider`. `SendTextMessageCommand` now calls provider.
- **GMR-004 Delivery State:** `ChatMessage` has `DeliveryState` enum (None, Pending, Sent, Failed); `ChatMessage` inherits `ObservableObject`. MainViewModel sets Pending → Sent/Failed on send. UI shows …/✓/✗ for User messages.
- **GMR-005 System Error Messages:** `ChatMessageType.System` added. `ErrorOccurred` handler adds System messages to chat; 3s debounce for rapid duplicate errors.
- **Files:** `IConversationProvider`, `OpenAIRealtimeService`, `GeminiLiveService`, `OpenAIConversationProvider`, `GeminiConversationProvider`, `MockConversationProvider`, `MainViewModel`, `ChatMessage`, `MainPage.xaml`, `ValueConverters.cs`.

### February 20, 2026 (GMR-009, GMR-010 Brain Context Pipeline – LLM Context Specialist)
- **Scope:** Shared context envelope builder with token budgeting per `BRAIN_CONTEXT_PIPELINE_SPEC.md` and `CHAT_V2_EXECUTION_TICKET_BOARD.md`.
- **Models:** BrainEvent, BrainEventType; SharedContextEnvelope; ContextAssemblyInputs (RecentChat, ActiveTarget).
- **IBrainContextService:** GetContextForVoiceAsync, GetContextForChatAsync; FormatAsPrefixedContextBlock for interim provider path.
- **BrainContextService MVP:** Builds envelope from recent chat + reel moments + active target; deterministic token budget (900 voice, 1200 chat, 1600 max); priority order: target metadata → chat (newest first) → reel refs (confidence desc); TruncationReport string; L2/L3 empty.
- **Integration:** Registered in MauiProgram; MainViewModel requests context before SendTextAsync and prepends context block to user message.
- **Files:** `Models/BrainEvent.cs`, `Models/SharedContextEnvelope.cs`, `Models/ContextAssemblyInputs.cs`, `Services/IBrainContextService.cs`, `Services/BrainContextService.cs`, `MauiProgram.cs`, `ViewModels/MainViewModel.cs`.

### February 20, 2026 (GMR-006 VisualReelService MVP – Capture Specialist)
- **Scope:** Capture track per `CHAT_V2_EXECUTION_TICKET_BOARD.md`.
- **ReelMoment model:** Id, TimestampUtc, SourceTarget, FrameRef, EventRef, Confidence (aligned with BRAIN_CONTEXT_PIPELINE_SPEC).
- **IVisualReelService:** Append, GetRecent(count); in-memory MVP.
- **VisualReelService:** Rolling retention — MaxCount 500, MaxAgeSeconds 300; deterministic trim on append.
- **Integration:** Registered in MauiProgram; MainViewModel appends moment on each FrameCaptured (source from CurrentTarget).
- **Files:** `Models/ReelMoment.cs`, `Services/IVisualReelService.cs`, `Services/VisualReelService.cs`, `MauiProgram.cs`, `ViewModels/MainViewModel.cs`.

### March 19, 2026 (Observation Store / Live Capture Stabilization Direction)
- **Scope:** Post-live-test architecture review for severe lag, stale-message flood, and recent-gameplay-memory design alignment.
- **Finding:** The existing capture path is too eager to turn accepted frames into brain requests and UI fanout. `VisualReelService` remains an in-memory MVP and is no longer sufficient for the product promise of a searchable recent gameplay memory.
- **Decision direction:** Evolve the reel into a bounded local observation store with SQLite metadata/index plus filesystem artifacts. Keep `BrainContextService` as the bounded short-term context seam. Add a novelty/salience gate between capture and brain submission so "captured" no longer implies "send to brain now".
- **Retention direction:** Align the observation-store hot window with `T18` so timeline, chat, ghost/UI, and recent gameplay artifacts all obey the same five-minute retention policy.
- **Artifacts updated:** `chronicles/DECISION_LOG.md`, `chronicles/OPEN_THREADS.md`, `WITNESS/AGENT_HANDOFF_INSTRUCTIONS.md`, `WITNESS/DEPLOYMENT_ROADMAP.md`, `WITNESS/gaimer_spec_docs/OBSERVATION_STORE_ARCHITECTURE.md`.

### December 16, 2025 (MainView V2 Draft – Swap-Ready Fixes)
- **Context:** Prepared `WITNESS/mainview-v2-design.xaml` to be a drop-in replacement for `MainPage.xaml` later (no swap performed yet).
- **Fixes / Adds:**
  - Removed invalid XAML (`StrokeThickness="0,0,0,1"` / per-side thickness) and replaced with layout-based dividers.
  - Removed `Label.Padding` usage (converted badge to `Border` + `Label`).
  - Added missing **MainPage functionality** to V2: minimize-to-MinimalView button, audio section (IN/OUT + visualizer), footer/connect button (with connecting spinner).
  - Rewired the main content area to existing `MainViewModel` bindings (`HasAiContent`, `HasNoAiContent`, `AiDisplayContent.Text`, `AiDisplayContent.ImageSource`).
- **Note:** V2 XAML now targets `x:Class="WitnessDesktop.MainPage"` for swap-in-place readiness; file currently remains in `WITNESS/` as a draft.

---

### December 16, 2025 (MainView V2 – Remaining UI Implementation Plan)
- **Added:** `WITNESS/gaimer_spec_docs/MAINVIEW_V2_UI_REMAINING_IMPLEMENTATION_PLAN.md`
- **Purpose:** Plan the remaining V2 design elements (chat feed with message types + chat input bar + mock/test scenarios) before full backend support.

---

### December 23, 2025 (Audio Device Selection Research)
- **Created:** `WITNESS/AUDIO_DEVICE_SELECTION_RESEARCH.md`
- **Purpose:** Comprehensive research on independent audio input/output device selection for desktop platforms
- **Scope:** Windows and macOS (MacCatalyst) - mobile platforms deferred
- **Key Findings:**
  - ✅ **Windows:** Fully supported via NAudio's device enumeration (`WaveInEvent.DeviceCount`, `WaveOutEvent.DeviceCount`)
  - ⚠️ **macOS/MacCatalyst:** Partial support via AVAudioSession routing (limited to system-provided options)
  - ✅ **WebSocket APIs:** No compatibility issues - device selection is purely client-side
  - ⚠️ **Bluetooth:** Works but adds 100-200ms latency and requires careful error handling
- **Implementation Approach:**
  - Windows: Trivial - just set `DeviceNumber` property on NAudio objects
  - MacCatalyst: Use `AVAudioSession.OverrideOutputAudioPort()` for speaker/Bluetooth switching
  - MacCatalyst input: System default only (user must configure in System Settings)
- **Proposed API Extensions:**
  - `GetAvailableInputDevicesAsync()` → `List<AudioDeviceInfo>`
  - `GetAvailableOutputDevicesAsync()` → `List<AudioDeviceInfo>`
  - `SetInputDeviceAsync(deviceId)` → Set active input device
  - `SetOutputDeviceAsync(deviceId)` → Set active output device
  - `AudioDevicesChanged` event for hot-plug detection
- **Estimated Effort:**
  - Phase 1 (Windows): 4-8 hours
  - Phase 2 (MacCatalyst): 8-12 hours
  - Phase 3 (Advanced features): 12-20 hours
  - Total: ~28-46 hours
- **Status:** Research complete, awaiting user decision on implementation priority

---

### February 20, 2026 (Chat V2 – Next Sprint Skills + Subagent Matrix)
- **Created:** `WITNESS/gaimer_spec_docs/NEXT_SPRINT_SKILLS_SUBAGENT_MATRIX.md`
- **Purpose:** PM orchestrator artifact mapping specialist roles, skills by sprint, and Cursor subagent types for Chat V2 + Brain Context Pipeline implementation
- **Scope:** Sprint 1 (MVP path) + Sprint 2 (reliability); 7 roles (PM, Frontend, Realtime/Infra, Capture, LLM Context, Chronicler, Showcaser)
- **Contents:** One-page action list, skills matrix (Sprint 1/2/nice-to-have), P0 ticket ownership, assignment matrix, handoff protocol

### February 23, 2026 (UI – Chat Input Redesign)
- **Chat Input Enlarged:** Replaced single-line `Entry` with multi-line `Editor` in `MainPage.xaml` chat input area to match the original design mockup.
  - Wrapped input in a bordered container (`BgSecondary` background, `#3d2e5a` stroke, rounded corners) with `MinimumHeightRequest="100"`
  - Editor auto-sizes with text (`AutoSize="TextChanges"`, max 120px height)
  - Send button (➤) repositioned to bottom-right inside the input border
  - Matches the taller, more prominent chat input from the previous design screenshot
- **Send Button Styled Yellow:** Send button (➤) now uses `AccentYellow` background with dark text, rounded corners, and transitions from 30% opacity (disabled/no text) to full opacity when `CanSendTextMessage` is true, giving clear visual feedback that the message is ready to send.
- **Files Modified:**
  - `src/WitnessDesktop/WitnessDesktop/MainPage.xaml` (chat input section, lines ~545-600)

### February 23, 2026 (Tooling & Skills – Agent Infrastructure)
- **Superpowers MCP Plugin Installed:** Added `@superpowers/mcp-server` to global Cursor MCP config (`~/.cursor/mcp.json`). Provides expert-crafted workflow skills (TDD, systematic debugging, code review, brainstorming, git worktree management) accessible via `find_skills`, `use_skill`, `get_skill_file` tools.
- **Code Reviewer MCP Research:** Evaluated options from Anthropic's official plugin directory (`code-review`, `pr-review-toolkit`) and community MCP servers (`code-review-mcp-server`, `claude-code-review-mcp`). Anthropic plugins are Claude Code–only; MCP-based options available for Cursor.
- **Frontend Design Aesthetic Skill Created:** `~/.cursor/skills/frontend-design-aesthetic/SKILL.md`
  - Adapted from Anthropic's official `frontend-design` plugin (Claude Code plugin directory)
  - Provides creative direction: design thinking, typography, color/theme, motion, spatial composition, atmosphere/texture
  - Anti-patterns for generic "AI slop" aesthetics
  - Context-specific adaptation table (Gaming UI, Dashboard, Marketing, Tool)
  - Explicit relationship to existing skills: `component-design-patterns` (how) + `implement-design` (what to match) + this skill (creative direction)
- **CURSOR_SUBAGENT_ROSTER Updated:** Frontend Engineer section updated with full skill stack.
- **Files Added:**
  - `~/.cursor/skills/frontend-design-aesthetic/SKILL.md` (new, personal Cursor skill)
- **Files Modified:**
  - `~/.cursor/mcp.json` (added Superpowers MCP server)
  - `WITNESS/PROGRESS_LOG.md`
  - `WITNESS/CURSOR_SUBAGENT_ROSTER.md`

---

**Notes:**
- Progress is tracked weekly
- Each phase has specific deliverables
- Milestones mark major completion points
- Risks are monitored and mitigated proactively

### March 20, 2026 (Capture/Brain Review Cleanup)
- **Brain pacing cleanup:** `OpenRouterBrainService.Dispose()` now disposes the shared image-analysis `SemaphoreSlim`, so the pacing gate no longer survives the service lifecycle.
- **Capture pipeline cleanup:** removed the redundant `admission.StoreObservation` wrapper in `MainViewModel`; non-stored frames already return before the store path.
- **Test hardening:** `ChannelPipelineTests` now waits for all three routed events rather than any checkpoint before asserting.
- **Verification:**
  - `dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst` passed.
  - `dotnet test src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0` hit an unrelated live failure in `StockfishLiveTests.Stockfish_AnalyzeOpeningPosition_ReturnsDepth15Plus` (`Depth` 13 vs expected `>= 15`).

### March 20, 2026 (GaimerScreenCapture xcframework Rebuild Fixed)
- **Root cause fixed:** `src/WitnessDesktop/NativeHelpers/GaimerScreenCapture/build-xcframework.sh` was using `xcodebuild -packagePath`, which this local Xcode does not support. The script now builds from the package directory with plain `xcodebuild build`.
- **Framework discovery improved:** the script now prefers the actual Catalyst output under `Build/Products/Release-maccatalyst/PackageFrameworks/`.
- **Fallback metadata corrected:** if the script ever has to synthesize a framework from a bare dylib, it now uses Catalyst-appropriate platform metadata instead of `MacOSX`.
- **Verification:**
  - `./src/WitnessDesktop/NativeHelpers/GaimerScreenCapture/build-xcframework.sh` succeeded.
  - A fresh `GaimerScreenCapture.xcframework` was copied into `src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/`.
  - `dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst` passed against the rebuilt helper.
- **Runtime implication:** the reduced native ScreenCaptureKit log noise in `GaimerScreenCapture.swift` is now buildable/deployable instead of being source-only.
