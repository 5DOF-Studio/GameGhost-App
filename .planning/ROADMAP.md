# Gaimer Desktop - Project Roadmap

## Overview
AI gaming companion desktop app (.NET 8 MAUI). Watches gameplay via screen capture and provides real-time voice/text insights through specialized AI agents.

---

### Phase 01: MainView V2 UI
**Status:** Complete
**Goal:** Redesigned main view with gamer aesthetic, agent selection, and capture preview
**Plans:** 1 plan
Plans:
- [x] 01-01-PLAN.md -- MainView V2 UI implementation

---

### Phase 02: Chat Brain Architecture
**Status:** Complete
**Goal:** Unified event routing system, timeline-based chat feed, session state machine, and proactive Brain alerts
**Plans:** 6 plans
Plans:
- [x] 02-01-PLAN.md -- Core Models
- [x] 02-02-PLAN.md -- Session State Machine
- [x] 02-03-PLAN.md -- Brain Event Router
- [x] 02-04-PLAN.md -- Timeline Feed Manager
- [x] 02-05-PLAN.md -- Chat Prompt Builder
- [x] 02-06-PLAN.md -- Timeline UI Component

---

### Phase 03: Screen Capture - ScreenCaptureKit Migration
**Status:** Complete
**Goal:** Migrate macOS capture from CGDisplayCreateImage to ScreenCaptureKit so GPU/Metal-rendered game windows are captured correctly
**Plans:** 3 plans
Plans:
- [x] 03-01-PLAN.md -- Build native Swift SCK helper xcframework
- [x] 03-02-PLAN.md -- Wire SCK into WindowCaptureService with fallback
- [x] 03-03-PLAN.md -- End-to-end verification with Apple Chess.app

---

### Phase 04: Ghost Mode — Native Floating Overlay
**Status:** Complete
**Goal:** Native NSPanel xcframework for macOS ghost mode — transparent floating FAB + event cards over the game. IGhostModeService abstraction for cross-platform support.
**Plans:** 3 plans
Plans:
- [x] 04-01-PLAN.md -- GaimerGhostMode Swift package + xcframework (NSPanel, pure AppKit views, @_cdecl exports)
- [x] 04-02-PLAN.md -- C# interop layer (IGhostModeService, DllImport, MacGhostModeService, DllImportResolver extension)
- [x] 04-03-PLAN.md -- Integration wiring (csproj NativeReference, DI registration, MainViewModel ghost mode toggle)

---

### Phase 05: Brain Infrastructure
**Status:** Complete
**Goal:** IBrainService + OpenRouter REST client for brain/worker LLM calls. BrainResult types, Channel<T> pipeline, tool execution runtime (capture_screen, get_game_state, get_best_move, web_search). Separate brain from voice provider.
**Research:** `GAIMER/GameGhost/gaimer_spec_docs/openrouter-integration-research.md`, `GAIMER/GameGhost/gaimer_spec_docs/voice-brain-concurrency-research.md`
**Plans:** 4 plans
Plans:
- [x] 05-01-PLAN.md -- Core types: BrainResult, OpenRouter DTOs, IBrainService interface, MockBrainService
- [x] 05-02-PLAN.md -- OpenRouter REST client + local tool executor
- [x] 05-03-PLAN.md -- BrainEventRouter Channel<T> consumer upgrade
- [x] 05-04-PLAN.md -- OpenRouterBrainService + DI wiring + MainViewModel integration

---

### Phase 06: Capture Pipeline — Brain-Voice Alignment
**Status:** Complete
**Goal:** Enforce brain-voice pipeline rules (core IP). Remove legacy voice-receives-images path. Implement IFrameDiffService (dHash with SkiaSharp) for smart frame submission. Build L1/L2 context layers in IBrainContextService. Wire voice to pull context from brain, not receive raw images. Capture precepts (Auto/Diff/OnDemand).
**Architecture Rule:** `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_VOICE_PIPELINE_RULES.md` (CANONICAL)
**Research:** `GAIMER/GameGhost/gaimer_spec_docs/image-diff-detection-research.md`, `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_CONTEXT_PIPELINE_SPEC.md`
**Plans:** 3 plans
Plans:
- [x] 06-01-PLAN.md -- IFrameDiffService + FrameDiffService (dHash perceptual hashing with SkiaSharp)
- [x] 06-02-PLAN.md -- IBrainContextService upgrade (L1 event store + L2 rolling summary)
- [x] 06-03-PLAN.md -- Pipeline enforcement (remove SendImageAsync, wire diff gate, L1 ingestion, DI)

---

### Phase 09: Stockfish Chess Engine — Local Analysis
**Status:** Complete
**Goal:** Replace Lichess cloud eval with local Stockfish engine. On-demand download when user selects chess agent. Dual-tool architecture: engine analysis (authoritative) + LLM strategic analysis (explanatory). FEN validation, UCI wrapper, download UX with "Chess Skills" framing.
**Research:** `memory/stockfish-integration.md`
**Plans:** 4 plans
Plans:
- [x] 09-01-PLAN.md -- IStockfishService + UCI wrapper + downloader + FEN validator + MockStockfishService
- [x] 09-02-PLAN.md -- Dual chess tools (analyze_position_engine + analyze_position_strategic) + ToolExecutor integration
- [x] 09-03-PLAN.md -- Agent system prompts + Chess Skill download UX + Stockfish lifecycle
- [x] 09-04-PLAN.md -- Integration verification + dev Stockfish download + live engine tests

---

### Phase 10: Integration & Orchestration Test Coverage
**Status:** Complete
**Goal:** Close end-to-end verification gaps identified by system audit. TDD approach to test MainViewModel orchestration, voice WebSocket services, conversation provider layer, DI registration, and critical integration seams (capture->brain->channel->router->voice). Prove the system works as a whole, not just as individual bricks.
**Audit Reference:** 301/301 unit tests passing but orchestration layer untested — MainViewModel (0 tests), voice services (0 tests), DI wiring (0 tests), conversation providers (0 tests).
**Plans:** 4 plans
Plans:
- [x] 10-01-PLAN.md -- Foundation: TestStubs fixes + pure logic tests (VoiceConfig, SettingsService, MockAuth, MockConversationProvider) 31 tests
- [x] 10-02-PLAN.md -- ConversationProviderFactory + SupabaseAuthService (provider selection logic + auth layer) 27 tests
- [x] 10-03-PLAN.md -- MainViewModel orchestration (lifecycle, events, capture->brain pipeline) 53 tests
- [x] 10-04-PLAN.md -- AgentSelection + voice service guards + integration seam verification 46 tests

---

### Phase 11: Agent Personality System
**Status:** Complete
**Goal:** Structured personality framework with per-agent SOUL/STYLE/BEHAVIOR/SITUATIONS/ANTI-PATTERNS sections. Personality injection into all LLM touchpoints (voice, brain, chat). Compose personality from human-authored design files into const strings at compile time. Close the brain-voice personality gap where brain writes generic text and voice reads it without character.
**Research:** `GAIMER/GameGhost/gaimer_spec_docs/agent-personality-reference/` (OpenClaw + aaronjmars/soul.md analysis), `GAIMER/GameGhost/gaimer_spec_docs/system-prompt.md` (Dross reference implementation)
**Builder Questionnaire:** `Agentic Office/RemoteAgents/Gaimer-Desktop-Brain/Agent Personality Builder — Leroy (Chess Master).md`
**Design Files:** `GAIMER/GameGhost/gaimer_spec_docs/agents/{leroy,wasp}/` (6 files each: SOUL, STYLE, BEHAVIOR, SITUATIONS, ANTI-PATTERNS, EXAMPLES)
**Plans:** 6 plans (3 waves)
Plans:
- [x] 11-01-PLAN.md -- Agent personality architecture (structured sections + ToolGuidanceBlock + Agent.cs restructure)
- [x] 11-02-PLAN.md -- ChatPromptBuilder agent-awareness (replace hardcoded Dross with agent personality)
- [x] 11-03-PLAN.md -- OpenRouterBrainService agent-awareness (personality prefix in brain prompts + AgentKey)
- [x] 11-04-PLAN.md -- Compose Leroy personality (from 64-question builder questionnaire)
- [x] 11-05-PLAN.md -- Compose Wasp personality (distinct queen archetype contrast)
- [x] 11-06-PLAN.md -- Integration verification (personality persistence across voice/brain/chat, 488 tests)

---

### Phase 07: Persistence Layer
**Status:** Deferred
**Goal:** SQLite + EF Core for chat history, session replay, and capture browser
**Plans:** 0 plans
Plans:
- [ ] TBD -- to be planned with /gsd:plan-phase

---

### Phase 08: Polish
**Status:** In Progress (4/5 plans complete)
**Goal:** Code review fixes (auth logging, API key hygiene, Shell.Current guards, caching), Voice Chat behavioral redesign (decouple mic from connection), Ghost Mode card reskin (indigo glass morphism), capture pipeline parameterization (CaptureConfig per agent), Settings UX improvements (gear icon, session restart on provider change). Audio visualizer (SkiaSharp) deferred to a future phase pending further research.
**Research:** `08-RESEARCH.md`
**Plans:** 5 plans (3 waves)
Plans:
- [x] 08-01-PLAN.md -- Code review fixes (Console.WriteLine, Disconnecting state, test improvements)
- [x] 08-02-PLAN.md -- Ghost Mode card reskin (indigo glass morphism)
- [x] 08-03-PLAN.md -- Voice Chat behavioral redesign (decouple connection from mic)
- [x] 08-04-PLAN.md -- Capture pipeline parameterization (CaptureConfig per agent)
- [ ] 08-05-PLAN.md -- Settings UX (gear icon + session restart on provider change)

---

### Phase 12: Audio Intelligence Pipeline
**Status:** Research Required
**Goal:** Wire up the four audio toggles (Voice Chat, Voice Command, Game Audio, Audio In) with real functionality. Each toggle represents a distinct audio pipeline with different AI backends, hardware requirements, and user-facing behaviors.

**Toggle Breakdown:**

1. **VOICE CHAT** (existing) — Real-time bidirectional voice via Gemini/OpenAI WebSocket. Already functional.

2. **VOICE COMMAND** — Local speech-to-text via Whisper model. User speaks, audio is transcribed locally, transcript sent to brain as text message, brain responds as chat message. Enables communication without entering full voice chat session.
   - Research: Whisper model download + local inference (whisper.cpp, CoreML, or ONNX Runtime)
   - Research: On-device model size vs accuracy tradeoffs (tiny/base/small)
   - Research: Streaming vs batch transcription for low-latency command recognition
   - Implementation: Download UX (similar to Stockfish "Chess Skills" pattern)
   - Implementation: IAudioTranscriptionService abstraction + WhisperTranscriptionService

3. **GAME AUDIO** — Capture audio from the target game window, transcribe it, and send to a "sound engineer" brain worker for autonomous processing. Brain produces audio summaries with timestamps for context enrichment (L1/L2 layers).
   - Research: macOS audio capture from specific window/app (ScreenCaptureKit audio, or virtual audio device)
   - Research: Windows WASAPI loopback capture per-process
   - Research: Sound classification vs pure transcription (game sounds, voice chat, music)
   - Implementation: IGameAudioCaptureService abstraction
   - Implementation: Sound engineer brain worker prompt + autonomous summary pipeline
   - Implementation: Timestamp-tagged audio context feeding into BrainContextService

4. **AUDIO IN** — Detect if user has a headset/mic connected. If so, emit AI voice output directly into the mic input stream as if the user were speaking. Replaces VoiceMod-style voice changers. AI becomes audible to other gamers in-game.
   - Research: macOS Core Audio — virtual audio device injection, aggregate devices, HAL plugins
   - Research: Windows — virtual audio cable, WASAPI render-to-capture loopback
   - Research: Legal/TOS implications for various game platforms
   - Research: Latency requirements for natural-sounding mic injection
   - Implementation: IVirtualMicService abstraction
   - **Potential network effects:** If AI can speak into game voice chat, other players hear the AI companion. This is a differentiator with viral potential.

**Dependencies:** Phase 05 (Brain Infrastructure), Phase 06 (Capture Pipeline), Phase 11 (Agent Personality)
**Plans:** 0 plans — requires research phase first
Plans:
- [ ] TBD -- research with /gsd:research-phase, then plan with /gsd:plan-phase

**Notes:**
- UI toggles are deployed now (buttons visible, not yet functional beyond Voice Chat)
- Deploy-first strategy: ship toggles, wire up from deployment
- Voice Command and Game Audio are the most achievable near-term
- Audio In (virtual mic injection) is the highest-risk, highest-reward feature
