# Changelog

All notable changes to gAImer Desktop are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Planned
- Phase 07: Persistence (session history, settings storage)
- Phase 12: Audio Intelligence Pipeline (voice command, game audio, virtual mic)
- Windows build and distribution

---

## [1.0.0-beta] - 2026-03-09

### Release — First Public Beta

All core systems verified end-to-end in live gameplay. Full brain pipeline (screen capture through AI analysis through voice narration) ran stable 10+ minutes against Apple Chess.app.

**Includes everything from v0.1.0 through v0.12.0:**
- 11 phases complete (01-06, 08-11 + live testing sprint)
- 519 unit & integration tests passing
- CI/CD via GitHub Actions
- macOS distribution: Developer ID signed, Apple-notarized, hardened runtime
- Notarized build: 39MB distributable zip (users unzip and drag to /Applications)
- Build script: `scripts/build-release-mac.sh` (automated publish → sign → notarize → staple → zip)

---

## [0.12.0] - 2026-03-07

### Milestone — Full Brain Pipeline Verified Live
- Complete pipeline running stable 10+ minutes against Apple Chess.app
- Screen capture → Claude Sonnet 4 (OpenRouter) → timeline → Gemini Live voice narration
- Brain model switched to Claude Sonnet 4 (`anthropic/claude-sonnet-4`) via OpenRouter
- JPEG MIME type fix (`image/jpeg`, was sending `image/png` for JPEG frames)
- Ghost panel positioning corrected for multi-monitor setups
- Relative timestamps now use consistent formatting

### Added — Live Testing Sprint (519 tests)
- `.env` file loading at startup for API key configuration
- Out-game `ChatAsync`: text chat works before connecting to a game session
- Enter-to-send keyboard shortcut in chat input
- Newest-first message ordering in timeline
- Error display system: `SystemError` events routed to timeline + ghost overlay
- Power button 3-state model: gray (disconnected) / green (connected) / red (error)
- 17 new tests for live testing fixes and error handling paths

### Added — Phase 08: UI Polish (Complete, 5/5 plans)
- Settings page with bento grid layout (Voice Config, Active Voice, System Info, About)
- Global error page (`ErrorPage.xaml`) with QueryProperty navigation
- Audio feature guards: per-agent `SupportsVoiceChat`/`VoiceCommand`/`GameAudio`/`AudioIn` flags
- Toggle snap-back when feature not supported by selected agent
- Custom app icon (1024x1024 PNG)
- Capture rate label unified: "Every 30s + on every move" across all surfaces
- `CaptureConfig` record with per-agent parameterization wired into pipeline

---

## [0.11.0] - 2026-03-06

### Added — CI/CD
- GitHub Actions workflow (`dotnet-test.yml`) for automated testing on push/PR
- Runs on ubuntu-latest via net8.0 target, SkiaSharp native deps included

### Added — UI Polish Sprint
- Audio panel overhauled: 4 toggles (VOICE CHAT, VOICE COMMAND, GAME AUDIO, AUDIO IN)
- Ghost FAB: agent portrait when connected, ghost hint system with Preferences persistence
- Power button: opens game selector when disconnected (second connect path)
- Timeline: relative timestamps (just now, Xs ago, Xm ago)
- Chat input + message bubbles reduced ~20% for better proportions
- Agent labels unified: Wasp "Chess Mistress" → "Chess Master"

### Fixed
- Toggle orientation: up=ON, down=OFF (was inverted)
- Stockfish engine path: use FileSystem.AppDataDirectory on Catalyst

---

## [0.10.0] - 2026-03-05

### Added — Login/Invite + Agent Onboarding
- OnboardingPage with 4-state flow (SignIn → AgentBrowse → Downloading → Ready)
- Dev auto-approve auth when SUPABASE_URL not configured
- Connect button flip animation (ScaleY + color swap)

### Added — MainPage V3 Layout Redesign
- Top bar: capture preview + live message window + disconnect/settings
- Sidebar: connectors only (preview moved to top bar)
- Bottom bar: agent info + inverted VAD + ghost button
- Ghost button 3-state model (disconnected/connected/ghost-active)
- Circular button design language across all controls
- Gun metal color purge (all violet/purple eliminated)

### Fixed — Bug Fix Sprint
- Catalyst window visibility: cleaned App.xaml.cs (~460 → ~85 lines), SceneDelegate lifecycle
- MainPage blackout after agent selection: set properties before BindingContext
- Chess connector toggle: binding fix + InputTransparent
- Loading spinner prevents flash during page transition

---

## [0.9.0] - 2026-03-04

### Added — Phase 11: Agent Personality System (30 tests)
- 5-block personality architecture (Soul/Style/Behavior/Situations/Anti-Patterns)
- Leroy personality composed from 64-question builder questionnaire
- Wasp personality composed as distinct contrast (queen archetype, positional)
- Voice providers use ComposedPersonality for full structured personality
- ChatPromptBuilder agent-aware (SOUL + BEHAVIOR for text chat)
- Brain personality injection via AgentKey → BrainPersonalityPrefix

### Fixed — Phase 08 Code Review
- SupabaseAuthService: Console.WriteLine → Debug.WriteLine, added IDisposable
- OpenAIRealtimeService: removed bare APIKEY/API_KEY fallback (cross-contamination)
- SettingsService: cached fallback device ID
- MainViewModel: Shell.Current null-guards at 6 disconnect points

---

## [0.8.0] - 2026-03-03

### Added — Phase 10: Integration Test Coverage (157 tests, 458 total)
- TestStubs fixes + VoiceConfig + SettingsService + MockAuth + MockConversationProvider
- ConversationProviderFactory tests (15) + SupabaseAuthService refactor + auth tests (12)
- MainViewModel orchestration tests (53): constructor, lifecycle, events, pipeline
- AgentSelectionVM (19) + voice service guards (21) + integration seams (6)

### Added — Phase 09: Stockfish Chess Engine (127 tests, 279 total)
- IStockfishService with FenValidator, UciParser, StockfishService, StockfishDownloader
- Dual chess tools: analyze_position_engine (Stockfish) + analyze_position_strategic (LLM)
- ChessToolGuidance in Leroy + Wasp system instructions
- "Chess Skills" download overlay in AgentSelectionPage with progress bar
- MockStockfishService with canned results for 3 positions

---

## [0.6.0] - 2026-03-03

### Added — Test Coverage (140/140 passing)
- Phase 1 critical path tests: 84 unit tests covering BrainEventRouter, BrainContextService, FrameDiffService, OpenRouterBrainService, ToolExecutor, VisualReelService, AudioResampler, SessionManager, TimelineFeed, ChatPromptBuilder
- Phase 2 easy path tests: 29 tests for Session & State (11), Timeline (11), Prompt Building (7)
- Phase 3+4 tests: 27 tests for Audio (11), Models/Serialization (10), Integration (6)
- Live API integration tests for Lichess cloud eval and OpenRouter chat completion
- Channel pipeline integration tests with concurrent producer-consumer validation
- End-to-end tests for BrainContextService envelope and SessionManager state cycles
- Test infrastructure: net8.0 library target, TestStubs.cs, Directory.Build.targets

### Fixed
- ToolDefinition ParametersSchema populated with real JSON Schema (was empty `"{}"` on all 6 tools)
- web_search: query parameter (required)
- player_history: username (required), game_type (optional)
- player_analytics: username (required), metric (optional)
- capture_screen, get_best_move, get_game_state: empty object (parameterless cache reads)

---

## [0.5.0] - 2026-03-02

### Added — Phase 06: Capture Pipeline
- IFrameDiffService with dHash perceptual hashing for frame deduplication
- Pipeline enforcement: removed legacy `SendImageAsync` from FrameCaptured handler
- Brain-only image consumption path (voice never receives raw images)
- L1 event store (0-30s immediate observations) in BrainContextService
- L2 rolling summary (30s-5min) in BrainContextService
- BrainEvent ingestion routed from BrainEventRouter to BrainContextService

### Added — Phase 05: Brain Infrastructure
- IBrainService abstraction with OpenRouterBrainService (production) and MockBrainService (dev)
- OpenRouterClient with vision analysis and multi-turn tool calling
- ToolExecutor for local brain tool execution (Lichess cloud eval, LLM web search)
- Channel&lt;BrainResult&gt; async producer-consumer pipeline
- BrainEventRouter Channel consumer integration
- DI wiring: IBrainService registered as singleton in MauiProgram.cs
- BrainResult types and OpenRouter DTOs with source-generated JSON serialization

---

## [0.4.0] - 2026-02-27

### Added — Phase 04: Ghost Mode (Native Floating Overlay)
- GaimerGhostMode.xcframework: NSPanel-based floating overlay for macOS
- Pure AppKit implementation (NSView + CALayer + Core Animation)
- 16 @_cdecl exports for C# interop (universal arm64+x86_64)
- vtool binary retagging to bridge macOS framework to Mac Catalyst runtime
- FAB button with agent portrait and yellow glow when connected
- Voice/text/image event card variants with styled backgrounds
- Click-through transparent areas (selective hit-test on ClickThroughHostingView)
- Auto-dismiss event cards with configurable timer and fade animations
- IGhostModeService abstraction (MacGhostModeService + MockGhostModeService)
- MAUI window hide/restore via NSWindow orderOut/makeKeyAndOrderFront

### Added — MainView UI Polish
- Audio control cards with toggle switches and LED VAD indicators
- Ghost FAB button integrated into MainPage footer
- Profile layout refinements

---

## [0.3.0] - 2026-02-25

### Added — Phase 03: Screen Capture (ScreenCaptureKit)
- GaimerScreenCapture.xcframework: native ScreenCaptureKit helper for macOS
- Swift Package with SCK native helper source
- P/Invoke declarations in NativeMethods.cs
- WindowCaptureService integration with fallback path
- DllImportResolver shared between both xcframeworks

---

## [0.2.0] - 2026-02-24

### Added — Phase 02: Chat Brain Architecture
- SessionContext with OutGame/InGame state machine
- Timeline hierarchy: Checkpoint > EventLine > TimelineEvent
- 11 generic EventOutputTypes + agent-specific enums (Chess, RPG, FPS)
- EventIconMap with 16 unique icons (no-reuse policy)
- ISessionManager with tool availability gating (3 out-game, 6 in-game)
- IBrainEventRouter central hub routing to Timeline, Voice, Ghost
- ITimelineFeed with checkpoint creation and event stacking
- IChatPromptBuilder with dynamic session context and tool instructions
- TimelineView.xaml with CollectionView and template selectors
- DirectMessageBubble.xaml and ProactiveAlertView.xaml
- Agent feature gating (Leroy available, Derek/Wasp gated)

### Added — Chat V2 MVP
- SendTextAsync on IConversationProvider (Gemini, OpenAI, Mock)
- Delivery state (Pending/Sent/Failed) on ChatMessage
- In-thread error messaging with 3s debounce
- VisualReelService with rolling retention (5min + max 500)
- SharedContextEnvelope builder with token budgeting (900 voice, 1200 chat, 1600 max)
- Offline-aware send with UI state binding

---

## [0.1.0] - 2026-02-23

### Added — Phase 01: MainView V2 UI & Foundation
- Agent Selection screen with card-based UI
- Main Dashboard with three-column layout (preview, AI insights, game selector)
- Minimal Connected View (960x350 wide format)
- Connection state machine (Disconnected/Connecting/Connected)
- Audio system: microphone capture + playback + resampling
- Voice provider abstraction (Gemini Live, OpenAI Realtime, Mock)
- Dark gamer aesthetic with Orbitron/Rajdhani fonts
- Custom color palette (cyan/purple accents)
