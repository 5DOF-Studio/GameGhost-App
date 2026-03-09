# Agent Handoff Instructions

**Project:** Gaimer Desktop (.NET MAUI)
**Date:** March 9, 2026
**Status:** ✅ Phase 01-06 Complete | ✅ Phase 08-11 Complete | ✅ CI Green on develop | ✅ All Tests Passing (519/519) | ✅ Brain Pipeline Verified End-to-End | ✅ v1.0.0-beta Notarized & Ready for Distribution

---

## v1.0.0-beta Distribution (March 9, 2026)

- **Tag:** `v1.0.0-beta` on main
- **Branches:** main and develop in sync at `f34aabf`
- **macOS Release build:** Notarized and stapled by Apple (submission ID: `c773e4ea-7d30-4fe9-bdc9-23270078e948`, status: Accepted)
- **Distribution zip:** `GaimerDesktop-notarized.zip` (39MB) — users unzip and drag to /Applications
- **Signing identity:** Developer ID Application: Ike Nlemadim (VW5K99T4JJ), hardened runtime enabled
- **Build script:** `scripts/build-release-mac.sh` — automated publish → sign → notarize → staple → zip
- **Entitlements:** `scripts/GaimerDesktop.entitlements` — screen capture, mic, network, JIT, native framework loading
- **Windows:** Not yet built — Ghost Mode Windows impl pending

### Repo Location Change

Repo moved from `~/Documents/5DOF Projects/gAImer/gAImer_desktop` to **`~/Developer/gAImer_desktop`** via fresh clone.

**Why:** `~/Documents/` is iCloud-managed — caused build timeouts, 2000+ duplicate files, slow git. Moving to `~/Developer/` dropped build time from 90s to 4s.

**Note:** Native xcframeworks (GaimerGhostMode, GaimerScreenCapture) are not in git. Copied from old repo. For fresh clones, rebuild from `src/GaimerDesktop/NativeHelpers/`.

---

## Current State Summary

| Milestone | Status |
|-----------|--------|
| Stage 0: Environment Setup | ✅ Complete |
| Stage 1: Repository Baseline | ✅ Complete |
| Stage 2: UI Build-Out (Mock) | ✅ Complete |
| Stage 3: Mock Services | ✅ Complete |
| Phase 01: MainView V2 UI | ✅ Complete |
| Phase 02: Chat Brain Architecture | ✅ Complete (Feb 24, 2026) |
| Phase 03: Screen Capture (SCK) | ✅ Complete (Feb 25, 2026) |
| Phase 04: Ghost Mode | ✅ Complete (Feb 27, 2026) |
| Phase 05: Brain Infrastructure | ✅ Complete (Mar 2, 2026) |

**What's Done:**
- Agent Selection, Main Dashboard, MinimalView - all functional
- Mock services for audio, capture, Gemini
- State management with Singleton MainViewModel
- Window resizing between views
- AI message display with auto-dismiss
- **Phase 02 Chat Brain Architecture** - FULLY IMPLEMENTED:
  - Core Models: SessionContext, MessageRole/Intent, Timeline hierarchy, BrainMetadata, ToolDefinition
  - Session State Machine: ISessionManager with OutGame/InGame states, tool gating
  - Brain Event Router: IBrainEventRouter wired to MainViewModel (OnScreenCapture, OnDirectMessage, OnImageAnalysis)
  - Timeline Feed Manager: ITimelineFeed with checkpoint creation, event stacking
  - Chat Prompt Builder: IChatPromptBuilder with dynamic system prompt assembly
  - Timeline UI: TimelineView.xaml with DataTemplateSelector, DirectMessageBubble, ProactiveAlertView (urgency styling)
- **Event icon assets organized** (16 unique icons for generic + agent-specific events)
- **Agent feature gating implemented** (Leroy-Chess available, others gated)
- **Phase 03 Screen Capture (SCK)** - FULLY IMPLEMENTED:
  - GaimerScreenCapture.xcframework with @_cdecl exports for ScreenCaptureKit
  - WindowCaptureService with SCK capture + CGDisplayCreateImage fallback
  - Metal/GPU game windows captured correctly (verified with Apple Chess.app)
- **Phase 04 Ghost Mode** - FULLY IMPLEMENTED & POLISHED (Feb 27 – Mar 2, 2026):
  - Native floating overlay (NSPanel) renders FAB + event cards over fullscreen games
  - MAUI window completely hidden via NSWindow.orderOut during ghost mode
  - Pure AppKit implementation (no SwiftUI -- SwiftUI is incompatible with Catalyst)
  - vtool binary retagging bridges macOS-built framework to Mac Catalyst runtime
  - GaimerGhostMode.xcframework with 18 @_cdecl exports (universal arm64+x86_64)
  - IGhostModeService abstraction with MacGhostModeService + MockGhostModeService
  - Click-through transparent areas via ClickThroughHostingView selective hit-test
  - FAB button with agent portrait, yellow glow when connected, toggles ghost mode
  - Gear badge (24pt) on FAB top-left — opens audio control card
  - Audio control card: 3 NativeToggleSwitches (MIC/AUTO/AI-MIC) with LED indicators, bidirectional C#↔native sync
  - Voice/text/image event card variants with auto-dismiss, balanced padding, X dismiss icon
  - Cards anchor LEFT of FAB with 14pt clearance, independent display/dismiss, dynamic repositioning on drag
  - Key bugs solved: SwiftUI NSHostingView symbol missing (switched to pure AppKit), DispatchQueue deadlock (DispatchQueue.main.async instead of @MainActor), UIWindow chrome visible (NSWindow.orderOut hides completely), card-to-FAB occlusion (right-align boundary + width shrink)

- **Phase 05 Brain Infrastructure** - FULLY IMPLEMENTED (Mar 2, 2026):
  - IBrainService interface with ChannelReader<BrainResult> pipeline
  - OpenRouter REST client (sync + SSE streaming + vision + tool calling)
  - ToolExecutor for local tool calls (capture_screen, get_game_state, get_best_move, web_search)
  - BrainEventRouter Channel consumer (StartConsuming/StopConsuming/RouteBrainResult)
  - OpenRouterBrainService: vision analysis + multi-turn tool calling (max 5 turns)
  - MockBrainService for dev/testing without API key
  - DI wiring: OPENROUTER_APIKEY env var selects production vs mock
  - MainViewModel capture forwarding to brain (parallel to voice, independent of voice connection)
  - Code review fixes: API key collision, CancelAll race condition, error sanitization, HttpClient DNS

- **Brain-Voice Pipeline Rules** - CODIFIED AS CORE IP (Mar 2, 2026):
  - Canonical spec: `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_VOICE_PIPELINE_RULES.md`
  - Brain is sole consumer of visual data — voice NEVER receives raw images
  - Voice receives text only via SendContextualUpdateAsync (push) or GetContextForVoiceAsync (pull)
  - Three-layer context model: L1 (immediate), L2 (rolling summary), L3 (session narrative)
  - Capture precepts: Auto (30s timer), Diff (dHash), OnDemand (voice tool request)
  - **DEVIATION RESOLVED:** Phase 06 removed legacy `SendImageAsync(compressed)` — Golden Rule now enforced in code

- **Phase 06 Capture Pipeline — Brain-Voice Alignment** - FULLY IMPLEMENTED (Mar 2, 2026):
  - IFrameDiffService: dHash perceptual hashing (64-bit, <0.5ms/frame, SkiaSharp 9x8 Gray8, 1.5s debounce)
  - IBrainContextService L1/L2 upgrade: L1 event store (30s window, 200 cap), L2 rolling summary (category-grouped), budget priority reorder
  - Pipeline enforcement: removed SendImageAsync from FrameCaptured, added HasChanged diff gate, L1 event ingestion from BrainEventRouter
  - DI wiring: IFrameDiffService singleton, IBrainContextService passed to BrainEventRouter
  - Verified: 10/10 must-haves passed, zero SendImageAsync call sites in capture pipeline

- **Test Infrastructure — Phase 1 Critical Path** - FULLY IMPLEMENTED & PASSING (Mar 3, 2026):
  - 82 unit tests across 10 files (Brain Pipeline 46 + Frame Analysis 14 + Context & Memory 22)
  - 84/84 test cases passing via `dotnet test -f net8.0` (~4 seconds, no IDE required)
  - net8.0 multi-target solution: both main project and test project dual-target net8.0 (library) + platform TFMs
  - TestStubs.cs provides ImageSource/MainThread stubs for net8.0 build (conditional on `#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS`)
  - MockHttpHandler pattern for testing concrete HttpClient dependencies (OpenRouterClient, Lichess API)
  - ReflectionHelper for testing private static methods (TruncateForVoice)
  - TestImageFactory for SkiaSharp gradient/checkerboard PNG generation (dHash requires pixel variation)
  - Remaining: Phase 2 (29 tests — Session, Timeline, Prompts) — now complete, Phase 3 (21 tests — Audio, Models), Phase 4 (6 integration tests)

- **Test Infrastructure — Phase 2 Easy Path** - FULLY IMPLEMENTED & PASSING (Mar 3, 2026):
  - 29 unit tests across 5 files (Session 11 + Timeline 11 + Prompts 7)
  - 113/113 total test cases passing via `dotnet test -f net8.0`
  - SessionManagerTests (8): state transitions, event firing, tool gating (3 OutGame / 6 InGame)
  - ToolDefinitionTests (3): JSON Schema validation, RequiresInGame flag correctness
  - TimelineFeedTests (8): checkpoint creation/prepend, event grouping by type, auto-checkpoint from session state
  - EventIconMapTests (3): icon/color/stroke coverage for all 11 generic EventOutputTypes
  - ChatPromptBuilderTests (7): core identity, behavior rules, in-game/out-game context, tool listing
  - **ToolDefinition ParametersSchema gap fixed:** Populated JSON Schema on all 6 tools (web_search has query, player_history/analytics have username, in-game tools have empty properties per spec)
  - Remaining: None — all planned test phases complete

- **Test Infrastructure — Phase 3 Trivial Path** - FULLY IMPLEMENTED & PASSING (Mar 3, 2026):
  - 21 unit tests across 5 files (Audio 11 + Models 10)
  - AudioResamplerTests (8): resample fast path, up/downsample counts, stereo-to-mono, float32-to-int16 clamping, null guard
  - AudioFormatTests (3): byte duration calculations, linear scaling
  - ContentConverterTests (5): polymorphic JSON read/write (string, array, null)
  - OpenRouterSerializationTests (3): round-trip with snake_case, null omission
  - BrainResultTests (2): default Priority=WhenIdle, default CreatedAt=UtcNow

- **Test Infrastructure — Phase 4 Integration** - FULLY IMPLEMENTED & PASSING (Mar 3, 2026):
  - 6 integration tests across 3 files
  - ChannelPipelineTests (2): multi-producer channel, BrainEventRouter channel consumer routing
  - EndToEndTests (2): BrainContextService full envelope (ingest→build→format), SessionManager full cycle
  - LiveApiTests (2): Lichess cloud eval real HTTP, OpenRouter chat completion real API (env var gated)
  - All 140/140 test cases passing

- **Showcase Agent Initialized** (Mar 3, 2026):
  - README.md created (project overview, architecture, build/test instructions, roadmap)
  - CHANGELOG.md created (v0.1.0-alpha through v0.6.0-alpha entries)
  - .github/workflows/dotnet-test.yml created (CI workflow for dotnet test on push/PR)
  - .gitignore updated with AI tool directories (.claude/, .codex/, .opencode/, .agents/, .axon/)
  - TDD skill installed (obra/superpowers@test-driven-development) for gaimer-desktop-engineer and gaimer-code-reviewer agents
  - gaimer-desktop-engineer and gaimer-code-reviewer agent CLAUDE.md files updated with TDD mandate
  - CI workflow could NOT be pushed to GitHub — PAT needs `workflow` scope (owner instruction created in Agentic Office)
  - All 140/140 tests still passing
  - Pushed to develop (commit b783bfd)

- **Phase 09 Plan 01: Stockfish Engine Service** - FULLY IMPLEMENTED WITH TDD (Mar 3, 2026):
  - New `Services/Chess/` directory with 6 production files
  - `IStockfishService.cs` — interface + `AnalysisOptions`, `EngineAnalysis`, `EngineVariation` records
  - `FenValidator.cs` — static FEN validation (ranks, pieces, king count, pawns, side to move, castling rights)
  - `UciParser.cs` — stateless UCI protocol parser (info depth lines with cp/mate/multipv, bestmove with ponder, nodes/time extraction)
  - `StockfishService.cs` — process management via `System.Diagnostics.Process`, UCI handshake (uci/uciok/isready/readyok), async analysis with `SemaphoreSlim`, cancellation via `stop` command, Threads=2/Hash=128 config
  - `StockfishDownloader.cs` — platform-aware binary download (macOS ARM64/x64, Windows x64), SHA256 verification, temp file + atomic rename, `chmod +x` on macOS
  - `MockStockfishService.cs` — canned results for 3 positions (starting, Sicilian, Scholar's mate), deterministic fallback (+30cp/e2e4)
  - TDD approach: RED (write tests) -> GREEN (implement) -> verified for each component
  - 53 new tests (20 FenValidator + 14 UciParser + 19 StockfishService/Mock/Downloader/Models)
  - **193/193 total tests passing, zero regressions**

- **Phase 09 Stockfish Chess Engine** - FULLY IMPLEMENTED (Mar 3, 2026):
  - Plan 01: IStockfishService + UCI wrapper + FenValidator + StockfishDownloader + MockStockfishService (53 tests)
  - Plan 02: Dual chess tools (analyze_position_engine + analyze_position_strategic) + ToolExecutor integration (35 tests)
  - Plan 03: ChessToolGuidance in Leroy/Wasp system instructions, "Chess Skills" download overlay, Stockfish lifecycle (26 tests)
  - Plan 04: End-to-end pipeline tests + live Stockfish 18 engine tests + build verification (13 tests)
  - 279/279 total tests passing, both net8.0 and maccatalyst builds clean

- **Phase 10 Integration & Orchestration Test Coverage** - FULLY IMPLEMENTED (Mar 3, 2026):
  - Plan 01: Foundation — TestStubs fixes + VoiceConfig + SettingsService + MockAuth + MockConversationProvider (31 tests)
  - Plan 02: ConversationProviderFactory (15 tests) + SupabaseAuthService refactor + auth tests (12 tests)
  - Plan 03: MainViewModel orchestration — constructor, lifecycle, events, pipeline (53 tests)
  - Plan 04: AgentSelectionVM (19) + voice service guards (21) + integration seams (6) = 46 tests
  - 458/458 total tests passing (157 new), zero regressions
  - Code review: 3 high-severity production findings (HttpClient leak, device ID logging, API key cross-contamination) deferred to Phase 08

- **Phase 11: Agent Personality System** - FULLY IMPLEMENTED (Mar 4, 2026):
  - Agent.cs restructured with 5 personality blocks: SoulBlock, StyleBlock, BehaviorBlock, SituationsBlock, AntiPatternsBlock
  - ToolGuidanceBlock (operational, separate from personality) + BrainPersonalityPrefix (~200 tokens)
  - ComposedPersonality computed property (cached via `??=`) composes all blocks with section headers
  - Leroy personality composed from 64-question builder questionnaire (cocky knight-obsessed wildcard)
  - Wasp personality composed as distinct contrast (composed queen archetype, positional, measured)
  - Voice providers (Gemini/OpenAI) use ComposedPersonality instead of SystemInstruction
  - ChatPromptBuilder agent-aware: uses SOUL + BEHAVIOR for text chat, falls back to Dross
  - Brain personality injection via AgentKey on SessionContext → BrainPersonalityPrefix
  - Design files: `GAIMER/GameGhost/gaimer_spec_docs/agents/leroy/` and `agents/wasp/` (SOUL, STYLE, BEHAVIOR, SITUATIONS, ANTI-PATTERNS, EXAMPLES)
  - 30 new personality tests (composition, per-agent, distinctiveness, tool guidance, backward compat, agent-awareness)
  - 488/488 total tests passing

- **Phase 08 Polish Fixes** - APPLIED (Mar 4, 2026):
  - SupabaseAuthService: Console.WriteLine → Debug.WriteLine, IDisposable, username redacted from logs
  - SettingsService: cached fallback device ID (_fallbackDeviceId ??= ...)
  - OpenAIRealtimeService: removed bare APIKEY/API_KEY fallback (cross-contamination fix)
  - MainViewModel: Shell.Current null-guards at 6 disconnect/navigation points
  - ComposedPersonality: cached with ??= pattern (code review fix)

- **Phase 08 Bug Fix Sprint** - APPLIED (Mar 5, 2026):
  - **Catalyst window visibility bug RESOLVED:** Cleaned App.xaml.cs from ~460 to ~85 lines (removed dead CGS, CGWindowList, RequestGeometryUpdate, ObjC interop code). Combined with SceneDelegate, AppDelegate, Program.cs state clearing from previous session.
  - **MainPage blackout after agent selection FIXED:** `IsPageReady` and `SelectedAgent` now set BEFORE `BindingContext` assignment in `OnNavigatedTo`. MAUI evaluates bindings immediately when BindingContext is set — properties must be ready first.
  - **Chess connector toggle FIXED:** Changed binding from `IsVoiceChatActive` to `IsConnected` (OneWay), added `IsInteractive="False"` + `InputTransparent="True"` on Switch, added `TapGestureRecognizer` on parent Border for `ShowWindowPickerCommand`.
  - **Loading spinner added:** Purple ActivityIndicator on dark background while `IsPageReady` is false, prevents faint-view-then-black flash during page transition.
  - **Ghost panel SetPosition API added (WIP):** `IGhostModeService.SetPosition(x, y)` wired through to native `ghost_panel_set_position`. NSScreen enumeration via ObjC P/Invoke for cross-screen positioning attempted but not working — deferred to draggable panel approach.
  - **502/502 tests passing** (14 new tests added during fixes)

- **UI Polish Sprint** - APPLIED (Mar 6, 2026):
  - Audio panel overhauled: 4 toggles (VOICE CHAT, VOICE COMMAND, GAME AUDIO, AUDIO IN), all caps, reordered, rose LED for AUDIO IN
  - Ghost FAB: shows agent portrait when connected (replaces text label), reduced 10pt, ghost hint system with Preferences persistence
  - Power button: opens game selector when disconnected (second connect path), keeps power icon
  - Toggle orientation fixed: up=ON, down=OFF (was inverted in IndustrialToggleSwitch)
  - Agent labels unified: Wasp "Chess Mistress" → "Chess Master" across all surfaces
  - Timeline: relative timestamps (just now, Xs ago, Xm ago), seconds tier to prevent duplicates, text reduced ~20%
  - Chat input reduced ~20% (editor font 30→24, send button 54→44, padding reduced)
  - DirectMessageBubble + ProactiveAlertView text reduced ~20%
  - Phase 12 (Audio Intelligence Pipeline) documented in ROADMAP

- **Phase 08 Final Polish** - APPLIED (Mar 6, 2026):
  - Settings page: bento grid card layout (4 cards: Voice Config, Active Voice, System Info, About)
  - Global error page: ErrorPage.xaml with dashed border, warning icon, error code badge, detail section, "Return Home" CTA
  - Audio feature guards: per-agent SupportsVoiceChat/VoiceCommand/GameAudio/AudioIn flags with toggle snap-back + DisplayAlert
  - App icon: custom 1024x1024 PNG embedded (macOS Dock shows cached icon until cache clear/logout)
  - Capture rate text unified: "Every 30s + on every move" across all surfaces
  - Onboarding chevrons hidden during Downloading/Ready states
  - Stockfish download path fixed: FileSystem.AppDataDirectory instead of SpecialFolder.ApplicationData
  - CI workflow: GitHub Actions green on develop (dotnet-test.yml)
  - 511/511 tests passing

- **Live Testing Sprint** - IN PROGRESS (Mar 6-7, 2026):
  - .env file loading: `LoadDotEnv()` in MauiProgram.cs, walks up dirs + `~/.gaimer/.env` fallback
  - Out-game text chat: `IBrainService.ChatAsync()` request-reply (personality-aware, 10-message history)
  - Enter-to-send: TextChanged event detects trailing `\n`, Shift+Enter for newline
  - Newest-first ordering: `Insert(0)` at all ChatMessages + EventLines call sites
  - Fixed UTC timestamps → `ToLocalTime()` in timeline
  - Voice-without-connection guard: DisplayAlert via `UnsupportedAudioFeatureToggled` event
  - Error display system: `EventOutputType.SystemError` + `IBrainEventRouter.OnError()` routes to timeline (gray capsule) + ghost card
  - Power button 3-state: gray (mock/offline) → green (live) → red (connected). `IsLive` checks ProviderName
  - 519/519 tests passing (8 new)

- **BRAIN PIPELINE VERIFIED END-TO-END** (Mar 7, 2026):
  - Full pipeline running stable for 10+ minutes against Apple Chess.app:
    **Screen Capture (ScreenCaptureKit) → Brain (Claude Sonnet 4 via OpenRouter) → Timeline display → Voice narration (Gemini Live)**
  - Brain reads chess board from screenshots, gives tactical advice, stays in Leroy personality
  - Voice mode works alongside brain pipeline without conflict
  - Text chat (out-game) uses gpt-4o-mini worker model, works perfectly with personality
  - **Bugs fixed (commit a8fc974):**
    1. **Brain model ID:** `anthropic/claude-sonnet-4-20250514` was wrong OpenRouter model ID (no Anthropic dashboard activity). Fixed to `anthropic/claude-sonnet-4`.
    2. **MIME type bug:** ImageProcessor outputs JPEG but `CreateImageAnalysisRequest` labeled it as `image/png`. Now auto-detects from magic bytes (JPEG FF D8 vs PNG 89 50).
    3. **Ghost panel off-screen:** FAB positioned off-screen, user couldn't return to MainView. Root cause: C# ObjC interop `objc_msgSend_stret` returns garbage on arm64 (Apple Silicon doesn't use stret variant). Fix: Swift-side auto-repositions panel to right edge of main screen on every `ghost_panel_show`.
    4. **Timestamps:** "just now" badge on all timeline items was meaningless (computed property never updates). Replaced with actual clock time (e.g., "1:19 am").
    5. **Error surfacing:** Brain errors now show HTTP status + model name instead of generic "Check logs" message.
  - **Observations from live testing:**
    - Brain hallucinated some board positions (vision model limitation, not code bug)
    - Brain doesn't track move list across captures — each analysis is stateless
    - Brain doesn't know which color the user is playing (window title has this info: "Tony Nlemadim - Computer (White to Move)")
    - OpenRouter audio models are REST-only (not WebSocket) — confirmed voice must stay on direct Gemini/OpenAI APIs
  - **Architecture confirmed:**
    - Voice: Gemini Live API / OpenAI Realtime API (direct WebSocket, not OpenRouter) — low-latency bidirectional audio
    - Brain: OpenRouter REST (anthropic/claude-sonnet-4 for vision, openai/gpt-4o-mini for text/tools)
    - Pipeline: Capture → ImageProcessor (50% scale, JPEG q60) → dHash diff gate → Brain → Channel<BrainResult> → BrainEventRouter → Timeline/Voice/Ghost

**What's NOT Done — Organized by Priority:**

### Priority 1: Distribution (IN PROGRESS)
- ✅ macOS notarized build ready (39MB zip, Apple-accepted)
- ✅ Build script: `scripts/build-release-mac.sh` (publish → sign → notarize → staple → zip)
- **Next:** Upload to Gaimer website with download button
- **Pending:** Windows build (Ghost Mode Windows impl needed, requires Windows machine or CI)

### Priority 2: Ghost Mode Improvements
- Research making ghost panel freely draggable (`NSPanel.isMovableByWindowBackground` or native drag handler)
- Ghost panel auto-repositions to right edge of main screen on show (Mar 7 fix), but still needs draggable support

### Priority 3: Brain Improvements (from live testing observations)
- Parse window title for user color (e.g., "Tony Nlemadim - Computer (White to Move)")
- Maintain move list in L2 context across captures (currently stateless per analysis)
- FEN extraction from brain output → Stockfish validation to reduce hallucinated positions
- Consider multi-shot prompting or providing previous analysis as context

### Priority 4: Deferred Phases
- **Phase 07: Persistence Layer** (SQLite, chat history, session replay)
- **Phase 12: Audio Intelligence Pipeline** (Voice Command/Whisper, Game Audio/SCK, Audio In/virtual mic — research required)
- **Windows Ghost Mode** (Win32 layered window overlay) — IGhostModeService interface ready, Windows impl pending

---

## Quick Start

### Prerequisites

1. **.NET 8.0 SDK** (version 8.0.412 or later)
   ```bash
   dotnet --version
   # Should show 8.0.xxx
   ```

2. **.NET MAUI Workloads** installed
   ```bash
   dotnet workload list
   # Should include: maui, maui-maccatalyst, maui-windows
   ```

3. **Xcode** (macOS only) - Required for MacCatalyst builds

---

## Build Commands

### macOS (MacCatalyst)

```bash
cd ~/Developer/gAImer_desktop

# Build for macOS (development - code signing disabled)
dotnet build src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false

# Deploy to /Applications and sign for development
rm -rf /Applications/GaimerDesktop.app
ditto --norsrc src/GaimerDesktop/GaimerDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/GaimerDesktop.app /Applications/GaimerDesktop.app
codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/GaimerDesktop.entitlements /Applications/GaimerDesktop.app
open /Applications/GaimerDesktop.app

# Release build (notarized)
./scripts/build-release-mac.sh
# Output: /tmp/gaimer-dist/GaimerDesktop-notarized.zip
```

**Notes:**
- The `-p:EnableCodeSigning=false` flag is required for development builds without an Apple Developer certificate.
- `dotnet run` may fail with "launch profile could not be applied" - use the direct binary path instead.
- Ignore the UIBackgroundModes warning - it doesn't affect functionality.

**⚠️ Known Issue - macOS Catalyst Window Sizing:**
On macOS Catalyst, the OS may restore a prior window size and MAUI window dimensions are in **device-independent units (points)**, not literal pixels. This can make the window appear “smaller” than expected even when Width/Height are set in code.

**Current approach (workaround):**
- Main window is **resizable**
- Starts at **1200×900**, with a **minimum** of **900×720**

### Windows

```bash
cd /path/to/gAImer_desktop/src/GaimerDesktop/GaimerDesktop

# Build for Windows
dotnet build -f net8.0-windows10.0.19041.0

# Run the app
dotnet run -f net8.0-windows10.0.19041.0
```

---

## Current Window Sizes

| View | Width | Height |
|------|-------|--------|
| Agent Selection | 1200px (default) | 900px (default) |
| Main Dashboard | 1200px (default) | 900px (default) |
| MinimalView (connected) | 960px | 350px |

---

## What to Test (Current Implementation)

### Complete Flow

1. **Agent Selection**
   - Launch app → Agent Selection screen appears (default 1200×900, resizable; min 900×720)
   - Select "General Gaimer" or "Chess Gaimer"
   - Should navigate to Main Dashboard

2. **Main Dashboard**
   - Verify agent badge shows selected agent
   - Select a game from the right panel
   - CONNECT button should enable
   - Click CONNECT → Should show spinner, then navigate to MinimalView

3. **MinimalView (Wide Format: 960×350)**
   - Window resizes to compact wide view
   - Header shows: Agent icon, name, game info, audio levels, expand button
   - Center shows: "Watching your game..." or AI message
   - Bottom shows: LIVE indicator, audio bars (static), DISCONNECT button

4. **AI Messages**
   - Mock service sends messages after connection
   - Messages appear centered in the content area (20pt font)
   - Messages auto-dismiss after 5 seconds
   - Tap anywhere in message area to dismiss manually

5. **Expand Flow**
   - Click expand button (⤢) → Returns to MainView
   - **Expected:** Connection remains active (not disconnected)
   - Window resizes back to default (1200×900, resizable; min 900×720)

6. **Disconnect Flow**
   - Click DISCONNECT → Returns to MainView
   - Window resizes to default (1200×900, resizable; min 900×720)
   - Connection status shows "OFFLINE"

---

## Project Structure

```
gAImer_desktop/
├── src/GaimerDesktop/GaimerDesktop/
│   ├── Views/
│   │   ├── AgentSelectionPage.xaml(.cs)
│   │   ├── MinimalViewPage.xaml(.cs)     # Wide format, inline messages
│   │   └── (MainPage.xaml in root)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs              # Singleton - shared state
│   │   ├── MinimalViewModel.cs           # UNUSED - kept for reference
│   │   └── AgentSelectionViewModel.cs
│   ├── Services/
│   │   ├── MockAudioService.cs
│   │   ├── MockWindowCaptureService.cs
│   │   ├── MockGeminiService.cs
│   │   └── I*.cs                         # Service interfaces
│   ├── Models/
│   │   └── SlidingPanelContent.cs
│   ├── App.xaml.cs                       # Window creation (resizable; default 1200×900, min 900×720)
│   └── MauiProgram.cs                    # DI configuration
├── GAIMER/GameGhost/
│   ├── PROGRESS_LOG.md
│   ├── FEATURE_LIST.md
│   ├── BUG_FIX_LOG.md
│   └── gaimer_spec_docs/
│       ├── GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md  # ✅ COMPLETE
│       ├── MINIMALVIEW_IMPLEMENTATION_TASK.md      # ✅ COMPLETE
│       └── gaimer_design_spec.md                   # Updated with current UI
└── GaimerDesktop.sln
```

---

## Architecture Notes

### State Management

**MainViewModel is registered as a Singleton** to share state between views:

- `AgentSelectionPage` → Transient ViewModel
- `MainPage` (Dashboard) → Uses singleton `MainViewModel`
- `MinimalViewPage` → **Directly binds to singleton `MainViewModel`**

This ensures connection state, audio levels, and AI content persist during navigation.

### MinimalView Design (Dec 12, 2024)

Layout (top to bottom):
1. **Header:** Agent icon, name, game info, audio levels, expand button
2. **Content:** Centered message display (inline, not sliding panel)
3. **Footer:** LIVE indicator | Audio bars | Disconnect button

---

## Known Issues

| Issue | Severity | Status | Notes |
|-------|----------|--------|-------|
| BUG-001: Audio bars not animating | Medium | Open | UI thread marshalling issue |
| macOS Catalyst window sizing | Low | Documented | Resizable main window; default 1200×900, min 900×720 |
| Audio visualizer static | Low | Expected | Animation is Phase 4 |

---

## Next Steps (Beyond Stage 3)

These are from `PROGRESS_LOG.md` and are **not part of Stages 1-3**:

### Window Capture (5% remaining)
- [ ] Real window enumeration (Windows EnumWindows / macOS CGWindowList)
- [ ] Real window capture (Windows PrintWindow / macOS CGWindowListCreateImage)
- See: `GAIMER/GameGhost/gaimer_spec_docs/SCREEN_CAPTURE_ARCHITECTURE.md` (architecture proposal)

### Phase 2: Audio — ✅ Code Complete
- [x] Microphone capture (WASAPI / AVAudioEngine)
- [x] Audio playback
- [x] Volume monitoring
- ⏳ Awaiting on-device validation

### Phase 3: Integration — ✅ Code Complete
- [x] Gemini WebSocket client
- [x] OpenAI Realtime client
- [x] Audio/image transmission
- [x] Response handling
- ⏳ Awaiting on-device validation

### Phase 02: Chat Brain — ✅ Complete
- [x] Core Models (SessionContext, Timeline, Events)
- [x] Session State Machine
- [x] Brain Event Router
- [x] Timeline Feed Manager
- [x] Chat Prompt Builder
- [x] Timeline UI Component

### Phase 03: Screen Capture — ✅ Complete
- [x] Native SCK Helper (GaimerScreenCapture.xcframework)
- [x] Screen Capture Service (C# P/Invoke + SCK integration with fallback)
- [x] Integration Verification (GPU/Metal capture confirmed with Apple Chess.app)

### Phase 04: Ghost Mode — ✅ Complete
- [x] Native Swift xcframework (GaimerGhostMode.xcframework, 16 @_cdecl exports, pure AppKit)
- [x] C# Interop Layer (IGhostModeService, DllImport, MacGhostModeService, MockGhostModeService)
- [x] Integration Wiring (csproj post-build copy, DI registration, MainViewModel ghost toggle + event forwarding)
- [x] FAB overlay button on MainPage (agent portrait, yellow glow, toggles ghost mode)
- [x] Click-through transparent areas, auto-dismiss cards, voice/text/image card variants

### Phase 05: Brain Infrastructure — ✅ Complete
- [x] Foundation Types (BrainResult, OpenRouter DTOs, IBrainService, MockBrainService)
- [x] OpenRouter REST Client + Tool Executor
- [x] BrainEventRouter Channel<T> Consumer Upgrade
- [x] OpenRouterBrainService + DI Wiring + MainViewModel Integration
- [x] Code Review Fixes (API key collision, CancelAll race, error sanitization)

### Next Phases
- **Phase 07: Persistence Layer** — SQLite schema, chat history, session replay (see design doc)
- **Phase 08: Polish** — Audio visualizer animation (SkiaSharp), error handling, code review fixes (HttpClient leak, device ID logging, API key cross-contamination)

---

## Useful Commands

```bash
# Clean build
dotnet clean -f net8.0-maccatalyst

# Restore packages
dotnet restore

# Build with verbose output
dotnet build -f net8.0-maccatalyst -p:EnableCodeSigning=false -v detailed

# Run all tests (519 test cases, ~11 seconds)
dotnet test src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0

# Skip live API tests (offline CI)
dotnet test src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0 --filter "Category!=LiveApi"

# Build tests only (no execution)
dotnet build src/GaimerDesktop/GaimerDesktop.Tests/GaimerDesktop.Tests.csproj -f net8.0

# Kill running app (if needed)
pkill -f GaimerDesktop
```

---

## Documentation References

| Document | Purpose |
|----------|---------|
| `GAIMER/GameGhost/PROGRESS_LOG.md` | Development timeline and phase status |
| `GAIMER/GameGhost/FEATURE_LIST.md` | Feature checklist with spec divergences |
| `GAIMER/GameGhost/BUG_FIX_LOG.md` | Bug tracking |
| `GAIMER/GameGhost/CURSOR_SUBAGENT_ROSTER.md` | Cursor specialist role definitions and invocation protocol |
| `GAIMER/GameGhost/gaimer_spec_docs/gaimer_design_spec.md` | UI/UX specifications (updated) |
| `GAIMER/GameGhost/gaimer_spec_docs/GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md` | Stages 1-3 plan (✅ Complete) |

---

---

## Distribution — READY

### macOS (Complete)
- **Signing identity:** Developer ID Application: Ike Nlemadim (VW5K99T4JJ)
- **Notarization credentials:** Stored as `GaimerNotary` keychain profile
- **Entitlements:** `scripts/GaimerDesktop.entitlements`
- **Build script:** `scripts/build-release-mac.sh` (or `--skip-notarize` for local testing)
- **First notarized build:** March 9, 2026 (submission c773e4ea, status: Accepted)
- **Output:** `/tmp/gaimer-dist/GaimerDesktop-notarized.zip` (39MB)
- Users download zip, extract, drag `GaimerDesktop.app` to `/Applications`

### Windows (Pending)
- Ghost Mode Windows impl needed (Win32 layered window, IGhostModeService interface ready)
- Requires Windows machine or Windows CI runner for build
- No code signing infrastructure set up yet

---

**Last Updated:** March 9, 2026
