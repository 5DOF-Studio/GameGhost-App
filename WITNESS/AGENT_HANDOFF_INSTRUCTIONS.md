# Agent Handoff Instructions

**Project:** Gaimer Desktop (.NET MAUI)
**Date:** March 21, 2026
**Status:** ✅ Phase 01-06, 08-11, 13-17 Complete | ✅ 1107 passed / 12 skipped / 0 failed (net8.0) | ✅ Brain Pipeline Verified E2E | ✅ v1.0.0-beta Notarized | ✅ Post-MiniCPM UX Sprint (8 slices) | ✅ Timeline Minute-Bucket Rearchitecture + Structured Brain Parsing + Chess Visibility Unification | ✅ Release Script Hardened (Mar 18) | ✅ Performance Hardening Sprint (Mar 21 — D-038 through D-048, 550MB→351MB, CA 182MB→38MB, CPU 2.9-8%)

---

## v1.0.0-beta Distribution (March 9, 2026)

- **Tag:** `v1.0.0-beta` on main
- **Branches:** main and develop in sync at `f34aabf`
- **macOS Release build:** Notarized and stapled by Apple (submission ID: `c773e4ea-7d30-4fe9-bdc9-23270078e948`, status: Accepted)
- **Distribution zip:** `WitnessDesktop-notarized.zip` (39MB) — users unzip and drag to /Applications
- **Signing identity:** Developer ID Application: Ike Nlemadim (VW5K99T4JJ), hardened runtime enabled
- **Build script:** `scripts/build-release-mac.sh` — automated publish → sign → notarize → staple → zip
  - `--skip-notarize` — signed but not notarized (quick local builds)
  - `--local-deploy` — sign + deploy to /Applications + launch (single command for testing)
- **Entitlements:** `scripts/WitnessDesktop.entitlements` — screen capture, mic, network, JIT, native framework loading
- **Windows:** Not yet built — Ghost Mode Windows impl pending

### Dev Build + Deploy Commands (Canonical)

The standard dev build, deploy, and launch sequence. Icons display correctly, TCC permissions persist across rebuilds, no re-prompting.

```bash
# Build
dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false

# Deploy to /Applications with Apple Development signing + entitlements
rm -rf /Applications/Gaimer.app
ditto --norsrc src/WitnessDesktop/WitnessDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/Gaimer.app /Applications/Gaimer.app
codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/WitnessDesktop.entitlements /Applications/Gaimer.app

# Launch
open /Applications/Gaimer.app

# One-liner (build + deploy + launch)
dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false && rm -rf /Applications/Gaimer.app && ditto --norsrc src/WitnessDesktop/WitnessDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/Gaimer.app /Applications/Gaimer.app && codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/WitnessDesktop.entitlements /Applications/Gaimer.app && open /Applications/Gaimer.app
```

**Why this works:** The `--force --deep` re-sign with a stable Apple Development identity ensures TCC (screen capture, microphone) permissions persist across rebuilds. Ad-hoc signing or running from build output directly will cause permission re-prompting.

**Note:** `dotnet publish -c Release` strips Assets.car differently — use the debug build path above for development. Release builds use `scripts/build-release-mac.sh` for notarized distribution.

### TCC Permissions Note (Mar 18)

If screen capture permissions stop working (app acts as if unsigned), reset TCC:
```bash
tccutil reset ScreenCapture
tccutil reset All com.5dof.gaimer
```
Then relaunch — macOS will prompt fresh for permissions.

### Local Deploy Entitlements Note (Mar 20)

Local `--local-deploy` installs were found to carry an invalid entitlements blob when `scripts/WitnessDesktop.entitlements` contained App Sandbox keys. macOS reported that blob would be ignored, which is a plausible cause of unstable permission behavior across local test installs. The local Developer ID entitlements file now contains only the non-sandbox runtime keys required by the app (`allow-jit`, `allow-unsigned-executable-memory`, `disable-library-validation`). For local permission verification, prefer a fresh deploy after this correction rather than reusing older `/Applications/Gaimer.app` installs.

### Capture Pressure Note (Mar 20)

The freeze diagnosed on March 20 was a CPU saturation problem, not a deadlocked idle app. Unified logging showed the live process repeatedly enumerating ScreenCaptureKit shareable windows and spamming native helper logs on each capture tick, with Gaimer running around 175% CPU during the freeze. The managed runtime now uses a true 5-second still-image baseline, performs diff/admission on the preview-sized JPEG instead of the full analysis artifact, and avoids creating timeline/reel entries for skipped frames. Windows capture was aligned to the same architecture afterward: one-shot timer/re-arm (no overlapping ticks), no pre-compress-before-emit step, and mock capture now honors the requested interval. OpenRouter image analysis now also has an explicit shared cooldown/serialization gate so auto-capture and on-demand `capture_screen` requests do not start back-to-back with zero spacing. The Swift `GaimerScreenCapture` source was also reduced to error-only logging, but that change does not affect runtime until the xcframework is rebuilt and recopied into the app.

### Repo Location Change

Repo moved from `~/Documents/5DOF Projects/gAImer/gAImer_desktop` to **`~/Developer/gAImer_desktop`** via fresh clone.

**Why:** `~/Documents/` is iCloud-managed — caused build timeouts, 2000+ duplicate files, slow git. Moving to `~/Developer/` dropped build time from 90s to 4s.

**Note:** Native xcframeworks (GaimerGhostMode, GaimerScreenCapture) are not in git. Copied from old repo. For fresh clones, rebuild from `src/WitnessDesktop/NativeHelpers/`.

---

## v1.0.0-beta Release (March 9, 2026)

- **Tag:** `v1.0.0-beta` on main
- **Branches:** main and develop in sync at `f34aabf`
- **Pushed:** main + develop + tag to GitHub
- **MacCatalyst build:** Succeeded (0 errors, 12 pre-existing warnings)
- **Tests:** Could not verify locally due to iCloud File Provider I/O timeouts (see below). CI on GitHub passes.
- **Changelog:** `[1.0.0-beta]` entry added
- **README:** Version badge added
- **Distribution signing:** Owner confirmed complete (Developer ID cert, notarization credentials, entitlements in repo)

### iCloud File Provider Issue (Active)

The repo lives in `~/Documents/` which is managed by iCloud File Provider. This causes:
- `Operation timed out` errors during `dotnet build` and `dotnet test` (writing to `obj/` dirs)
- Extremely slow git operations (indexing, merge, status)
- 2,038 iCloud conflict duplicate files (`" 2.*"` pattern) — **deleted this session**
- MAUI Resizetizer SVG processing timeouts

**Workarounds used this session:**
- Clear resizetizer cache: `rm -rf .../obj/Debug/net8.0-maccatalyst/maccatalyst-x64/resizetizer`
- Clear test obj/bin: `rm -rf .../WitnessDesktop.Tests/obj .../WitnessDesktop.Tests/bin`
- MacCatalyst build succeeded after cache clear; net8.0 test build still timed out

**Permanent fix:** Move repo outside `~/Documents/` (e.g., `~/Developer/`) to avoid File Provider entirely, or restart machine to reset iCloud sync state.

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

## Immediate Product Rotation

- Active shipping path is cloud-first. LocalFirst research is paused pending stronger hardware.
- Near-term distribution wedge is a single game-agnostic commentator agent, not specialist coaching agents.
- Keep the first tool surface minimal: `game_journal` plus LLM/web search.
- Position the agent as observant, curious, and personality-led. Do not overstate durable learning or game expertise until those systems are truly implemented.
- Treat remaining LocalOnly/LocalFirst references as shipping hazards. Any user-visible local runtime/settings/agent surface that survives into the next sprint must either be behind a feature gate with a clear "not active in this build" alert or removed entirely.
- Immediate refinement tracks for the next sprint:
  - audit and gate/remove remaining local-first user-visible references
  - ✅ bounded live working set — T18 5-min wall-clock retention with 60s sweep (D-043, Mar 21)
  - ✅ local observation store for recent gameplay memory (SQLite metadata + filesystem artifacts, Mar 19)
  - settings-triggered full app restart for structural config changes
  - instant-feeling toggles with separate pending indicators
  - tool-call visibility in the event timeline
  - reduce capture pressure with a 5s still-image baseline plus explicit on-demand capture
  - evolve the observation store contract so short clips (30s-60s) become first-class observations for high-speed agents instead of continuous brain-fed video

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
  - **Mar 20 runtime policy change:** default still capture cadence reduced to 5 seconds, with `capture_screen` retained as the explicit fresh-view tool path
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
  - Canonical spec: `WITNESS/gaimer_spec_docs/BRAIN_VOICE_PIPELINE_RULES.md`
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
  - Design files: `WITNESS/gaimer_spec_docs/agents/leroy/` and `agents/wasp/` (SOUL, STYLE, BEHAVIOR, SITUATIONS, ANTI-PATTERNS, EXAMPLES)
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

- **March 11, 2026 Live Session Stabilization** - APPLIED:
  - **Voice provider selection fix:** `ConversationProviderFactory` now honors `ISettingsService.VoiceProvider` before API-key auto-detect, so the selected backend in Settings is the backend that gets instantiated when both Gemini and OpenAI keys are present.
  - **OpenAI voice response fix:** added `OpenAiRealtimeSessionOptions` and changed `OpenAIRealtimeService` to send a canonical `session.update` payload with `turn_detection.create_response = true` and `turn_detection.interrupt_response = true`. This targets the reported symptom: mic starts, audio chunks send, but no AI response audio/text returns.
  - **Screen capture semantics fix:** added `CaptureEmissionGate` and wired it into MacCatalyst, Windows, and mock capture services. Runtime behavior now matches the documented promise "every 30s or when the image changes" by polling up to 1Hz and emitting on first frame, image change, or interval expiry.
  - **Tests added:**
    - `ConversationProviderFactoryTests.Create_AutoDetect_BothKeys_UsesSettingsProviderPreference`
    - `OpenAiRealtimeSessionOptionsTests.BuildSessionUpdateJson_EnablesServerVadAutoResponse`
    - `CaptureEmissionGateTests` (first frame, unchanged before interval, changed before interval, unchanged after interval)
  - **Verification:**
    - `dotnet test src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0 --filter "FullyQualifiedName~ConversationProviderFactoryTests|FullyQualifiedName~OpenAiRealtimeSessionOptionsTests|FullyQualifiedName~CaptureEmissionGateTests"` → 21/21 passing
    - `dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false` → succeeded
    - Note: plain MacCatalyst build with signing enabled still hit a workspace-specific codesign/xattr failure on the generated `.app`, but compilation itself succeeded.
  - **Open thread:** immediate disconnect after connect is still unconfirmed; retest after the voice fix before treating it as a separate bug.
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
- **GhostFab native AppKit rebuild spec written** (Mar 21) — off-site implementation at `docs/superpowers/specs/2026-03-21-ghostfab-native-rebuild-spec.md`
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
dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false

# Deploy to /Applications and sign for development
rm -rf /Applications/WitnessDesktop.app
ditto --norsrc src/WitnessDesktop/WitnessDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/WitnessDesktop.app /Applications/WitnessDesktop.app
codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/WitnessDesktop.entitlements /Applications/WitnessDesktop.app
open /Applications/WitnessDesktop.app

# Release build (notarized)
./scripts/build-release-mac.sh
# Output: /tmp/gaimer-dist/WitnessDesktop-notarized.zip
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
cd /path/to/gAImer_desktop/src/WitnessDesktop/WitnessDesktop

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
├── src/WitnessDesktop/WitnessDesktop/
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
├── WITNESS/
│   ├── PROGRESS_LOG.md
│   ├── FEATURE_LIST.md
│   ├── BUG_FIX_LOG.md
│   └── gaimer_spec_docs/
│       ├── GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md  # ✅ COMPLETE
│       ├── MINIMALVIEW_IMPLEMENTATION_TASK.md      # ✅ COMPLETE
│       └── gaimer_design_spec.md                   # Updated with current UI
└── WitnessDesktop.sln
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
- See: `WITNESS/gaimer_spec_docs/SCREEN_CAPTURE_ARCHITECTURE.md` (architecture proposal)

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
dotnet test src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0

# Skip live API tests (offline CI)
dotnet test src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0 --filter "Category!=LiveApi"

# Build tests only (no execution)
dotnet build src/WitnessDesktop/WitnessDesktop.Tests/WitnessDesktop.Tests.csproj -f net8.0

# Kill running app (if needed)
pkill -f WitnessDesktop
```

---

## Documentation References

| Document | Purpose |
|----------|---------|
| `WITNESS/PROGRESS_LOG.md` | Development timeline and phase status |
| `WITNESS/FEATURE_LIST.md` | Feature checklist with spec divergences |
| `WITNESS/BUG_FIX_LOG.md` | Bug tracking |
| `WITNESS/CURSOR_SUBAGENT_ROSTER.md` | Cursor specialist role definitions and invocation protocol |
| `WITNESS/gaimer_spec_docs/gaimer_design_spec.md` | UI/UX specifications (updated) |
| `WITNESS/gaimer_spec_docs/GAIMER_IMPLEMENTATION_PLAN_STAGE1-3.md` | Stages 1-3 plan (✅ Complete) |

---

---

## Distribution — READY

### macOS (Complete)
- **Signing identity:** Developer ID Application: Ike Nlemadim (VW5K99T4JJ)
- **Notarization credentials:** Stored as `GaimerNotary` keychain profile
- **Entitlements:** `scripts/WitnessDesktop.entitlements`
- **Build script:** `scripts/build-release-mac.sh` (or `--skip-notarize` for local testing)
- **First notarized build:** March 9, 2026 (submission c773e4ea, status: Accepted)
- **Output:** `/tmp/gaimer-dist/WitnessDesktop-notarized.zip` (39MB)
- Users download zip, extract, drag `WitnessDesktop.app` to `/Applications`

### Windows (Pending)
- Ghost Mode Windows impl needed (Win32 layered window, IGhostModeService interface ready)
- Requires Windows machine or Windows CI runner for build
- No code signing infrastructure set up yet

---

- **Phase 13 Ghost FAB Unified Message Card** - FULLY IMPLEMENTED (Mar 9, 2026):
  - UnifiedMessageCard.swift replaces EventCardNSView + AudioControlCardView (net -496 lines Swift)
  - Blue glass aesthetic (#121C3A @ 82% alpha, blue gradient border)
  - Collapsible ToolSectionView (4 audio toggles matching MainPage: VOICE CHAT/COMMAND/GAME AUDIO/AUDIO IN)
  - VadBarView (12 vertical CALayer bars with green/yellow/red color zones)
  - Voice card: agent avatar + bold green phone icon (24pt), VAD bars below
  - Alert mode (no auto-dismiss, X = OK), voice card 500ms hold-off before dismiss
  - C# P/Invoke bridge: ShowCard with isAlert/isVoiceDelivered, SetVadLevel(float), 4-param SetAudioState
  - MainViewModel: VAD forwarding (throttled 66ms), 4-toggle mapping, new IsAudioInActive property
  - 13 new tests, 529/529 total passing
  - Visual verification: 3 iterations with owner feedback applied
  - Known issues from testing: in-game chat broken, brain not seeing captures, AI hallucinating board advice, gear toggle lag

- **Test assertion fixes** - APPLIED (Mar 9, 2026):
  - 5 ContextBadge tests: aligned to current h:mm tt format (was expecting relative time)
  - 2 OpenRouter error tests: aligned to "Brain error" / "failed" message formats
  - 529/529 all green (was 519 + 6 stale failures)

- **Phase 14: Chess Context Engineering** - FULLY IMPLEMENTED (Mar 10, 2026):
  - ITelemetryService + ConsoleTelemetryService for structured pipeline observability (optional null-default injection)
  - Removed dead Lichess fallback — Stockfish-only, proper error JSON on not-ready
  - GameJournalService: in-memory move journal (200 cap), game_journal tool, BrainEventRouter ingestion, FEN extraction
  - BrainPromptBuilder: system/user message split, connection-state awareness, L1 events (capped at 5)
  - Brain vision prompt rewrite: identity, illusion language rules ("I can see the board"), tool awareness
  - Auto new-game detection: starting FEN comparison (board part only), >2 entries threshold, journal/context reset
  - In-game text chat routed to brain SubmitQueryAsync (bypasses voice WebSocket, full context: journal, tools, L1)
  - 648/648 tests passing (119 new across 6 plans)

- **Phase 15: Capture Pipeline Fix** - FULLY IMPLEMENTED (Mar 10, 2026):
  - Fixed critical _activeTasks counter leak: Interlocked.Increment moved INSIDE Task.Run delegate, CancellationToken.None for scheduling
  - Channel(1, DropOldest) frame slot: TrySubmitFrame always succeeds, latest frame wins, DropOldest callback for telemetry
  - ConsumeFramesAsync consumer loop: sequential frame processing, error recovery without dying
  - Same counter fix applied to SubmitQueryAsync
  - MockBrainService updated to match production Channel pattern
  - MainViewModel FrameCaptured handler rewired to TrySubmitFrame (removed IsBusy gate)
  - Frame lifecycle telemetry: frame_queued, frame_replaced, frame_dropped, frame_processing_start, frame_error
  - Code review fix: Consumer loop uses parameterless ReadAllAsync() — survives CancelAll() (service is DI singleton)
  - Code review fix: MockBrainService.Dispose waits for consumer task (parity with production)
  - **Known issues #1 (in-game chat) and #2 (brain not seeing captures) — BOTH FIXED**
  - 698/698 tests passing (50 new: frame slot, counter regression, MockBrainService, MainViewModel capture, consumer-survives-CancelAll)

- **Phase 16: Vision Context Engineering** - FULLY IMPLEMENTED (Mar 10, 2026):
  - **Anti-hallucination strategy** across 5 plans:
    - Plan 01: CoT `visual_description` field forces grounding, UNREADABLE escape hatch, confidence calibration tags
    - Plan 02: TopStripUpdated event on IBrainEventRouter — live activity bar now updates on every brain result
    - Plan 03: Brain model switched from Claude Sonnet 4 to Gemini 2.5 Flash (Google-only provider routing)
    - Plan 04: Structured JSON output via `response_format` schema, BrainAnalysisResult model, free-text fallback
    - Plan 05: Temporal consistency validation (duplicate FEN + piece count diff > 2), telemetry tracking
  - **Code review fixes:** Triple-parsing eliminated (single variable), JsonDocument leak fixed (using var), ConsoleTelemetryService Console.WriteLine → Trace.WriteLine
  - 744/744 tests passing (46 new)

  **Live Test Issues (Mar 7) → Phase 16 Mitigations:**

  | # | Issue | Status | Mitigation | Expected Effect |
  |---|-------|--------|------------|-----------------|
  | 1 | Brain hallucinated FEN positions | MITIGATED | CoT visual_description, UNREADABLE escape hatch, confidence tags | Must describe before analyzing, can't confabulate |
  | 2 | Brain returned free-text (inconsistent parsing) | SOLVED | response_format JSON schema with strict: true | Every response is typed JSON |
  | 3 | Live activity bar never updated | SOLVED | TopStripUpdated event, MainViewModel subscription | Updates on every brain result |
  | 4 | Claude Sonnet vision inconsistent/slow | SOLVED | Switched to google/gemini-2.5-flash | Faster, cheaper, better vision |
  | 5 | Duplicate/impossible FEN in journal | SOLVED | ValidateTemporalConsistency + telemetry | Bad FENs flagged, still recorded |
  | 6 | L1 confidence hardcoded 0.8 | SOLVED | Extracted from structured confidence field | Real model confidence (0.3-0.95) |
  | 7 | Capture pipeline frame leak (Phase 15) | SOLVED | Counter fix + Channel(1, DropOldest) | No more leaked frames |

  **Voice provider selection for live test:**
  - Set `GEMINI_APIKEY` env var → auto-detects Gemini (preferred over OpenAI)
  - Or explicit: `VOICE_PROVIDER=gemini` or `VOICE_PROVIDER=openai`
  - Voice name resolved automatically from agent gender (Leroy=Fenrir, Wasp=Kore)

- **March 11-12, 2026 Live Testing Sprint** - MAJOR PROGRESS:

  **Fixes Applied:**
  1. **APIKEY collision root fix (BUG-009):** Removed prefixed `AddEnvironmentVariables("GEMINI_"/"OPENAI_"/"OPENROUTER_")` from MauiProgram.cs. Kept only unprefixed `AddEnvironmentVariables()`. Fixes collision once at config construction. All key resolution uses explicit names only.
  2. **OpenAI voice verified working:** `insufficient_quota` error resolved (quota replenished). OpenAI Realtime producing real audio responses (audio:4800 bytes in live test).
  3. **OpenAI error surfacing:** `ProcessResponseDone()` parses `response.done` status, surfaces failed responses via `ErrorOccurred` event as "Voice service error".
  4. **Gemini Live protocol rewrite (by Codex):** Model updated to `gemini-2.5-flash-preview-native-audio-dialog`, serialization to camelCase, input schema to `realtimeInput.audio/text/video`, connection waits for `setupComplete` via TaskCompletionSource. Gemini still not operational (API issues).
  5. **File-based logging:** `TeeTextWriter` in MauiProgram.cs mirrors `Console.Out`/`Console.Error` to `/tmp/gaimer-debug.log`. Solves GUI-launched Catalyst app log visibility (stdout invisible when launched via `open`).
  6. **Brain service observability:** All `Debug.WriteLine` in `OpenRouterBrainService` upgraded to `Console.WriteLine` — brain pipeline activity now visible in log file.
  7. **dHash resolution fix (BUG-010):** 9x8 dHash (64-bit) was too coarse for chess — single piece moves produced distance=0. Added variable-width high-res dHash (33x32 = 1024-bit) via `CaptureConfig.DiffHashWidth`. Chess agents use 33, default stays 9. Move frames now produce distances of 4-23, correctly passing threshold=4.
  8. **dHash distance observability (by Codex):** `GetDistanceFromLast(byte[], int hashWidth)` non-mutating inspection method. Capture logs show actual distance values at decision point.

  **Live Test Results (Mar 12, 00:12 AM):**
  - ✅ Voice: OpenAI Realtime connected, Leroy personality loaded, voice=ash, bidirectional audio working
  - ✅ Capture: SCK firing, frames captured successfully (4MB PNGs)
  - ✅ Diff gate: High-res dHash detecting chess moves (distance 4-23 on moves, 0-2 on static board)
  - ✅ Brain: OpenRouterBrainService receiving frames, completing analysis, tool calls firing (analyze_position_engine)
  - ⚠️ Brain output quality: `**VISUAL DESCRIPTION:**` CoT markers leaking to UI (structured JSON parse failing, raw text fallback)
  - ⚠️ Voice grounding: Brain results reaching timeline but voice still mostly ungrounded (hallucinating chess advice)
  - ⚠️ Gemini voice: Not operational (API model issues, using OpenAI as fallback)

  **New Tests Added:**
  - `ApiKeyCollisionTests.cs` — 3 tests for APIKEY collision scenarios
  - `GeminiLiveProtocolTests.cs` — Gemini protocol contract tests
  - `OpenAiRealtimeProtocolTests.cs` — OpenAI protocol tests
  - `LiveApiTests.cs` — live API tests (skip when keys unavailable)
  - `AgentChessTests.cs` — DiffThreshold regression test
  - `FrameDiffServiceTests.cs` — GetDistanceFromLast non-mutation test
  - Updated MainViewModelTests for 3-arg HasChanged overload
  - **776/776 tests passing**

  **Known Issues At That Time (Mar 12 snapshot):**
  - **CoT format leak (BUG-011):** `**VISUAL DESCRIPTION:**` markers in timeline when BrainAnalysisResult JSON parse fails. Fix: strip markers in fallback path.
  - **Gemini voice not operational:** API model `gemini-2.5-flash-preview-native-audio-dialog` — needs verification against current API availability.
  - **Voice-brain grounding gap:** Brain results reach timeline but voice context injection path needs verification.

  **Architecture Confirmed (Mar 12):**
  - Voice: OpenAI Realtime API (working), Gemini Live API (protocol updated, not yet operational)
  - Brain: OpenRouter REST (google/gemini-2.5-flash for vision), tools via openai/gpt-4o-mini
  - Diff: Agent-scoped dHash — chess uses 33x32 (1024-bit), general uses 9x8 (64-bit)
  - Logging: /tmp/gaimer-debug.log via TeeTextWriter (visible even for GUI-launched apps)
  - Config: .env loading via LoadDotEnv(), VOICE_PROVIDER=openai for explicit provider selection

- **March 13, 2026 Rotation Update — Local research paused, cloud path active again:**

  **Decision:** Stop LocalOnly/LocalFirst live optimization on this machine. Phase 17 local infrastructure remains in the branch, but the product path is back on cloud providers until higher-quality hardware is available.

  **What changed during the local research pass:**
  1. **Local voice routing fix:** `ConversationProviderFactory` now prioritizes `InferenceMode.LocalOnly` over `VOICE_PROVIDER` env vars and settings provider selection.
  2. **Text timeline fix:** direct text messages now show on the visible timeline surface, not only in `ChatMessages`.
  3. **Speech TCC fix:** Mac Catalyst build now injects `NSSpeechRecognitionUsageDescription` into the generated app bundle plist because MAUI dropped it during merge.
  4. **Local timeout expansion:** local Ollama clients now default to a 10-minute timeout (`GAIMER_LOCAL_TIMEOUT_MINUTES` override) to avoid false "cancelled" UI during debugging.

  **Local research conclusion:**
  - CLI validation confirmed Ollama + `minicpm-v` is functional but too slow on current hardware (~16.4s for a trivial "hello" prompt, CPU-only).
  - Local voice remained non-viable after TCC was fixed: mic frames flowed, but STT/chunking behavior and near-silent input levels blocked usable conversation.
  - Local vision/reasoning on CPU is not acceptable for real-time product UX on this machine.
  - **Do not spend more time optimizing LocalOnly here. Treat that road as closed until better hardware is available.**

  **Active live-test rotation from now on:**
  - Use cloud providers for both voice and brain during live testing.
  - Preferred current configuration:
    - Brain: OpenRouter REST (`google/gemini-2.5-flash` vision, `gpt-4o-mini` worker/tools)
    - Voice: OpenAI Realtime as the stable default
    - InferenceMode: `CloudOnly`
  - If settings change a structural runtime choice (provider family / inference mode), prefer an app rebootstrap flow from Settings rather than a shallow session restart.

  **What to test next (cloud only):**
  1. Startup provider selection logs are correct.
  2. Typed text appears immediately and receives a cloud reply.
  3. Voice connect / speak / interrupt / playback works end-to-end.
  4. Capture → brain → timeline → voice grounding is verified.
  5. `GaimerScreenCapture.xcframework` bundling issue is resolved or explicitly deferred.

  **Settings restart direction:**
  - Structural runtime changes (inference mode, provider-family wiring) should no longer aim for a shallow session restart.
  - Preferred product behavior: `Apply Changes` in Settings saves the new configuration, then the app performs or requests a full restart.
  - Only use in-app rebootstrap if it becomes demonstrably clean and reliable.

  **Immediate distribution direction:**
  - Near-term distribution should center on a single game-agnostic commentator agent rather than specialist coaching agents.
  - Tool scope should stay intentionally narrow: `game_journal` + LLM/web search.
  - Position the agent as observant, funny, curious, and scene-aware, not as a specialist coach.
  - This reduces launch risk and fits the current cloud-first product path better than shipping domain-expert promises too early.

  **UX refinement added to future work:**
  - Toggle responsiveness needs a structural pass. Current connector UX is tied to effective backend state (`IsConnected`) rather than immediate user intent, which makes the control feel laggy on tap.
  - Desired behavior: toggle flips immediately on user tap; pending async work is shown separately through a flashing/pulsing indicator or other pending affordance.
  - Apply the same design principle to audio toggles (voice chat / voice command / commentary / audio in): immediate visual acknowledgment first, backend-confirmed state second.
  - Tool-call visibility should be added to the timeline. `ToolDefinition` already has `Name` + `Description`, and `ToolCallInfo` captures runtime details, but there is no dedicated timeline event or icon/display metadata yet. Future work should make tool execution legible to the user.
  - Long-session architecture needs a bounded live working set. Keep only the recent hot window in memory, virtualize/summarize older state, and move change gating earlier in the capture path.
  - Live capture should be redesigned around a bounded local observation store. Recent gameplay memory must live in app-owned local storage with explicit retention and retrieval semantics; brain analysis should run on selected observations instead of acting as the primary archive.

  **Current architecture direction (Mar 19):**
  - Use a local observation store as the source of truth for the last five minutes of gameplay.
  - Store metadata/index in SQLite and media artifacts on disk under `FileSystem.AppDataDirectory`.
  - Treat still images as phase 1 artifacts and short video clips as phase 2 artifacts under the same observation schema.
  - Add a novelty/salience gate between capture and brain submission so "captured" no longer implies "send to brain now".
  - Keep the brain working set bounded and app-owned (`BrainContextService` extension), with recent accepted visual state + recent emitted insights for repeat suppression.

  **March 20 review cleanup:**
  - `OpenRouterBrainService.Dispose()` now disposes the shared image-analysis pacing semaphore.
  - `MainViewModel` no longer carries the redundant `admission.StoreObservation` wrapper around the observation-store write.
  - `ChannelPipelineTests` now waits for all three expected routed events instead of any checkpoint.
  - Validation status after this cleanup:
    - Mac Catalyst build passed.
    - `dotnet test -f net8.0` surfaced an unrelated live Stockfish assertion (`Depth` 13 vs expected `>= 15`) in `StockfishLiveTests`, not a regression in the capture/pacing changes.
  - `GaimerScreenCapture/build-xcframework.sh` is no longer blocked:
    - removed the unsupported `xcodebuild -packagePath` usage
    - confirmed the script now rebuilds a valid `ios-arm64_x86_64-maccatalyst` xcframework
    - copied the rebuilt helper into `Platforms/MacCatalyst/GaimerScreenCapture.xcframework`
    - native ScreenCaptureKit log suppression from `GaimerScreenCapture.swift` is now ready to ship in the app on the next deploy/relaunch

**Last Updated:** March 12, 2026
