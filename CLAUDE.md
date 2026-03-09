# Project: gaimer-desktop

## Quick Reference
- Solution: `GaimerDesktop.sln`
- Project dir: `src/GaimerDesktop/GaimerDesktop/`

### Build
```bash
# macOS (MacCatalyst)
dotnet build src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false

# Windows
dotnet build -f net8.0-windows10.0.19041.0
```

### Run (macOS)
```bash
# Deploy to /Applications with Apple Development signing + entitlements
# Required for ScreenCaptureKit TCC permissions (ad-hoc signing doesn't work)
rm -rf /Applications/GaimerDesktop.app
ditto --norsrc src/GaimerDesktop/GaimerDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/GaimerDesktop.app /Applications/GaimerDesktop.app
codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/GaimerDesktop.entitlements /Applications/GaimerDesktop.app
open /Applications/GaimerDesktop.app

# One-liner build + run
dotnet build src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false && rm -rf /Applications/GaimerDesktop.app && ditto --norsrc src/GaimerDesktop/GaimerDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/GaimerDesktop.app /Applications/GaimerDesktop.app && codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/GaimerDesktop.entitlements /Applications/GaimerDesktop.app && open /Applications/GaimerDesktop.app
```

### Release Build (macOS — notarized)
```bash
./scripts/build-release-mac.sh
# Output: /tmp/gaimer-dist/GaimerDesktop-notarized.zip (39MB)
# Users unzip and drag to /Applications
```

### Run (Windows)
```bash
dotnet run -f net8.0-windows10.0.19041.0
```

## Architecture
.NET 8.0 MAUI app targeting MacCatalyst + Windows. AI gaming companion that watches gameplay via screen capture and provides real-time voice/text insights.

Three AI agents: Leroy (Chess, available), Derek (RPG, gated), Wasp (Chess Master, available).

### Key Patterns
- **MainViewModel is Singleton** — shared state across all views
- **MVVM** with DI via `MauiProgram.cs`
- **IConversationProvider** abstraction for AI backends (Gemini, OpenAI, Mock)
- **IBrainService** abstraction for analytical LLM pipeline (OpenRouter, Mock)
- **Brain Event Router** pattern — routes AI output to Timeline, Voice, UI
- **Timeline hierarchy** — Checkpoint > EventLine > TimelineEvent
- **Session State Machine** — OutGame/InGame with tool gating
- **Channel<BrainResult>** — async producer-consumer bridge between brain and router

### Project Structure
```
src/GaimerDesktop/GaimerDesktop/
  Views/           — XAML pages (AgentSelection, MainPage, MinimalView, Timeline, DirectMessageBubble, ProactiveAlert)
  ViewModels/      — MainViewModel (singleton), AgentSelectionViewModel
  Models/          — Agent, ChatMessage, SessionContext, BrainMetadata, ToolDefinition
  Models/Timeline/ — EventOutputType, TimelineCheckpoint, EventLine, TimelineEvent, EventIconMap
  Services/        — BrainEventRouter, SessionManager, TimelineFeed, ChatPromptBuilder
  Services/Brain/  — IBrainService implementations (OpenRouterBrainService, MockBrainService, OpenRouterClient, ToolExecutor)
  Services/Conversation/ — IConversationProvider, Gemini/OpenAI/Mock providers
  Controls/        — Custom controls (PlusPatternBackground)
  Utilities/       — Converters, TemplateSelectors
  Resources/       — Images, Fonts (Orbitron, Rajdhani), Styles
```

## Conventions
- Dark theme with gamer aesthetic (cyan/purple accents)
- Custom fonts: Orbitron-Bold (headers), Rajdhani (body)
- Conventional commits: `feat()`, `fix()`, `docs()`, `chore()`
- kebab-case for asset files, PascalCase for C# files
- Interfaces prefixed with `I` (IBrainEventRouter, ISessionManager)

## Key Paths
- Source: `src/GaimerDesktop/GaimerDesktop/`
- Planning: `.planning/` (GSD phases and plans)
- Docs: `GAIMER/GameGhost/` (handoff, feature list, progress log, spec docs)
- Assets: `assets/` (source PNGs), `Resources/Images/` (MAUI runtime)
- Spec docs: `GAIMER/GameGhost/gaimer_spec_docs/`

## Build Notes
- macOS: `-p:EnableCodeSigning=false` required for dev builds
- macOS: `dotnet run` and direct binary execution both fail from ~/Documents due to File Provider extended attributes. Use the ditto + ad-hoc sign workaround above.
- Ignore UIBackgroundModes warning on macOS
- macOS Catalyst may restore prior window size (known platform behavior)

## Brain-Voice Pipeline Rules (CRITICAL — Core IP)

**Canonical spec:** `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_VOICE_PIPELINE_RULES.md`

The brain is the SOLE CONSUMER of visual data. Voice NEVER receives raw images.

```
Capture → Brain ONLY → Channel<BrainResult> → Router → Timeline / Voice (text) / Ghost
```

### Hard Rules — Violating These Breaks the Architecture
1. **Voice receives text only** — via `SendContextualUpdateAsync(string)`, never image bytes
2. **Brain is sole image consumer** — `IBrainService.SubmitImageAsync()` is the only path for captured frames
3. **All brain output flows through Channel** — no direct writes to timeline or voice
4. **Voice pulls context** — via `GetContextForVoiceAsync()` for L1/L2/L3 layered memory
5. **Voice tools read brain cache** — `get_game_state`, `get_best_move` are instant reads, not live requests
6. **`capture_screen` tool triggers brain** — voice requests analysis, brain captures + analyzes, result flows back as text

### Prohibited Patterns
- `_conversationProvider.SendImageAsync(imageBytes)` from capture pipeline — REMOVE THIS
- Voice calling LLM directly for image analysis
- Bypassing Channel<BrainResult> to write directly to timeline
- Sending serialized BrainResult objects to voice (voice gets VoiceNarration text only)

### Three-Layer Context Model
- **L1 (0-30s):** Immediate observations from latest brain analysis
- **L2 (30s-5min):** Rolling summary of recent game behavior
- **L3 (5min+):** Session narrative arc
- Budget: Voice=900 tokens, Chat=1200, Hard max=1600

## Native Framework Constraints (CRITICAL)
- GaimerGhostMode is built for macOS (needs AppKit NSPanel), then vtool-retagged to Mac Catalyst
- **SwiftUI CANNOT be used** in macOS-built frameworks loaded into Catalyst — NSHostingView symbol doesn't exist in Catalyst's iOS SwiftUI
- All Ghost Mode UI must use pure AppKit (NSView + CALayer + Core Animation)
- GaimerScreenCapture is built for Mac Catalyst directly (no AppKit, no SwiftUI issue)
- DllImportResolver in NativeMethods.cs handles both frameworks; only registered once per assembly

## Key Spec Docs
- `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_VOICE_PIPELINE_RULES.md` — **CANONICAL** brain-voice pipeline architecture rules (core IP)
- `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_CONTEXT_PIPELINE_SPEC.md` — Three-layer context model (L1/L2/L3), SharedContextEnvelope
- `GAIMER/GameGhost/gaimer_spec_docs/chat-brain-design.md` — Engineering spec (data models, prompts, event flow)
- `GAIMER/GameGhost/gaimer_spec_docs/voice-brain-concurrency-research.md` — Dual pipeline architecture, Channel<T> design
- `GAIMER/GameGhost/gaimer_spec_docs/implementation-roadmap.md` — V1 (cloud) → V2 (local AI) master roadmap
- `GAIMER/GameGhost/gaimer_spec_docs/brain-and-tools-reference.md` — Voice agent tool schemas
- `GAIMER/GameGhost/gaimer_spec_docs/system-prompt.md` — Dross personality prompt

## Session Continuity
- `GAIMER/GameGhost/AGENT_HANDOFF_INSTRUCTIONS.md` — primary handoff document
- `GAIMER/GameGhost/PROGRESS_LOG.md` — development timeline
- `GAIMER/GameGhost/FEATURE_LIST.md` — feature checklist with spec divergences
- `.planning/STATE.md` — GSD phase status

## Current Status
- Phase 01 (MainView V2 UI): Complete
- Phase 02 (Chat Brain Architecture): Complete
- Phase 03 (Screen Capture): Complete
- Phase 04 (Ghost Mode): Complete
- Phase 05 (Brain Infrastructure): Complete — IBrainService, OpenRouter client, Channel pipeline
- Phase 06 (Capture Pipeline): Complete — dHash diff gate, L1/L2 context layers, pipeline enforcement
- Phase 09 (Stockfish Chess Engine): Complete — local Stockfish UCI, dual chess tools, 127 tests
- Phase 10 (Integration Test Coverage): Complete — 157 new tests (458 total), orchestration layer verified
- Phase 11 (Agent Personality System): Complete — 5-block personality architecture, Leroy + Wasp composed, 30 tests (488 total)
- Phase 08 (Polish): Complete — 5/5 plans. Settings bento grid, error page, audio feature guards, app icon, capture rate unified, onboarding chevron fix, CI workflow live. 511/511 tests passing.
- Live Testing Sprint: Brain pipeline verified end-to-end (Mar 7, 2026). Capture → Brain (anthropic/claude-sonnet-4) → Timeline → Voice (Gemini Live) ran stable 10+ min against Apple Chess.app. 5 bugs fixed: model ID, MIME type auto-detect, ghost panel positioning (arm64 stret fix), clock timestamps, error surfacing. 519/519 tests passing.
- Deployment: macOS Release build notarized by Apple (Mar 9, 2026). `scripts/build-release-mac.sh` automates publish → sign → notarize → staple → zip. Output: 39MB distributable zip.
- Repo moved to `~/Developer/gAImer_desktop` (iCloud File Provider fix — build 90s→4s)
- Next: Upload to website, Windows build, ghost mode dragging, brain improvements, Phase 07 (Persistence), Phase 12 (Audio Intelligence Pipeline)
